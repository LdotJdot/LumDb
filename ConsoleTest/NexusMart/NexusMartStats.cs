using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ConsoleTest.NexusMart
{
    internal enum OpKind : byte
    {
        PlaceOrder, Pay, Ship, Complete, Return, Cancel,
        Catalog, TicketNew, TicketUpd, TicketDel,
        CouponIssue, CouponUse, UserAdd, Audit,
        Find, FindMany, Count, Scan, Batch, Other
    }

    internal sealed class Agg
    {
        public long Ok;
        public long Fail;
        public long Miss;
        public long Ticks;
        public long TicksMin = long.MaxValue;
        public long TicksMax;
        public readonly int[] Buckets = new int[10];

        public void Add(long ticks, bool ok, bool miss)
        {
            if (ok) Interlocked.Increment(ref Ok);
            else Interlocked.Increment(ref Fail);
            if (miss) Interlocked.Increment(ref Miss);
            Interlocked.Add(ref Ticks, ticks);
            long min, max;
            do { min = Volatile.Read(ref TicksMin); }
            while (ticks < min && Interlocked.CompareExchange(ref TicksMin, ticks, min) != min);
            do { max = Volatile.Read(ref TicksMax); }
            while (ticks > max && Interlocked.CompareExchange(ref TicksMax, ticks, max) != max);
            Interlocked.Increment(ref Buckets[BucketOf(ticks)]);
        }

        public static int BucketOf(long ticks)
        {
            double us = ticks * 1_000_000.0 / Stopwatch.Frequency;
            if (us < 50) return 0;
            if (us < 100) return 1;
            if (us < 250) return 2;
            if (us < 500) return 3;
            if (us < 1000) return 4;
            if (us < 2500) return 5;
            if (us < 5000) return 6;
            if (us < 10000) return 7;
            if (us < 25000) return 8;
            return 9;
        }

        public static readonly string[] BucketLabels =
        [
            "<50μs", "50-100μs", "100-250μs", "250-500μs", "500μs-1ms",
            "1-2.5ms", "2.5-5ms", "5-10ms", "10-25ms", "≥25ms"
        ];

        public long Total => Volatile.Read(ref Ok) + Volatile.Read(ref Fail);
        public double AvgMs
        {
            get
            {
                long n = Total;
                if (n == 0) return 0;
                return Volatile.Read(ref Ticks) * 1000.0 / Stopwatch.Frequency / n;
            }
        }

        public double MinMs => Total == 0 || TicksMin == long.MaxValue ? 0 : TicksMin * 1000.0 / Stopwatch.Frequency;
        public double MaxMs => Total == 0 ? 0 : TicksMax * 1000.0 / Stopwatch.Frequency;
    }

    internal sealed class LaneAgg
    {
        public string Name = "";
        public int Seed;
        public bool Reader;
        public long ElapsedTicks;
        public long OpsDone;
        public readonly ConcurrentDictionary<OpKind, Agg> Ops = new();
        public readonly Agg Wait = new();
        public readonly Agg All = new();
    }

    internal sealed class SimStats
    {
        public readonly ConcurrentDictionary<string, LaneAgg> Lanes = new();
        public readonly ConcurrentDictionary<OpKind, Agg> Ops = new();
        public readonly ConcurrentQueue<string> Errors = new();
        public readonly List<(string name, double ms)> Phases = new();
        public double WallMs;
        public long HeartbeatOps;

        public LaneAgg Lane(string name, int seed, bool reader)
        {
            return Lanes.GetOrAdd(name, _ => new LaneAgg { Name = name, Seed = seed, Reader = reader });
        }

        public void Record(LaneAgg lane, OpKind kind, long ticks, bool ok, bool miss = false)
        {
            lane.All.Add(ticks, ok, miss);
            var a = lane.Ops.GetOrAdd(kind, _ => new Agg());
            a.Add(ticks, ok, miss);
            var g = Ops.GetOrAdd(kind, _ => new Agg());
            g.Add(ticks, ok, miss);
            Interlocked.Increment(ref lane.OpsDone);
            Interlocked.Increment(ref HeartbeatOps);
        }

        public void Wait(LaneAgg lane, long ticks)
        {
            lane.Wait.Add(ticks, true, false);
        }

        public void Error(string lane, Exception ex)
        {
            if (Errors.Count > 24) Errors.TryDequeue(out _);
            Errors.Enqueue($"[{lane}] {ex.GetType().Name}: {ex.Message}");
        }

        public string BuildReport(SimConfig cfg, AuditResult audit, string dbPath, long dbBytes, long memKb)
        {
            var sb = new StringBuilder(16_000);
            void L(string s = "") => sb.AppendLine(s);

            L("════════════════════════════════════════════════════════════════════");
            L("  云栈商城 NexusMart · 运营中台实例模拟报告");
            L("════════════════════════════════════════════════════════════════════");
            L($"  种子            {cfg.Seed}");
            L($"  复现命令        ConsoleTest --seed={cfg.Seed} --ops={cfg.Ops} --users={cfg.Users} --skus={cfg.Skus}{(cfg.Mem ? " --mem" : "")}{(cfg.Serial ? " --serial" : "")}");
            L($"  引擎            {(cfg.Mem ? "共享内存" : "磁盘临时库")}   调度={(cfg.Serial ? "串行回放" : "多路并发")}");
            L($"  库路径          {dbPath}");
            L($"  库大小          {FormatBytes(dbBytes)}    进程工作集 ≈ {memKb:N0} KB");
            L($"  主数据          用户 {cfg.Users} / SKU {cfg.Skus} / 预热订单 {cfg.Warmup}");
            L($"  车道规模        写 {cfg.WriterOps} ops/车道 × 写车道  · 读 {cfg.ReaderOps} ops/车道 × 读车道");
            L();
            L("说明：各车道内部的随机动作序列由「主种子 ⊕ 车道号」决定，可复现。");
            L("      并发时 OS 锁调度顺序不确定，因此最终库快照允许跨次运行有差异；");
            L("      使用 --serial 可得到同一种子下稳定的库校验值。");
            L();

            L("── 阶段耗时 ────────────────────────────────────────────────────────");
            L($"  {"阶段",-22} {"耗时",14} {"占比",10}");
            double totalMs = Phases.Sum(p => p.ms);
            foreach (var (name, ms) in Phases)
            {
                double pct = totalMs <= 0 ? 0 : ms * 100.0 / totalMs;
                L($"  {name,-22} {ms,12:N1} ms {pct,9:N1}%");
            }
            L($"  {"合计（阶段）",-22} {totalMs,12:N1} ms");
            if (WallMs > 0)
                L($"  {"墙钟（含编排）",-22} {WallMs,12:N1} ms");
            L();

            L("── 车道明细 ────────────────────────────────────────────────────────");
            L($"  {"车道",-22} {"种子",12} {"角色",6} {"完成",8} {"失败",8} {"未命中",8} {"耗时",12} {"等待事务(avg)",14} {"op avg",10}");
            foreach (var lane in Lanes.Values.OrderBy(v => v.Name))
            {
                long fail = 0, miss = 0;
                foreach (var op in lane.Ops.Values)
                {
                    fail += op.Fail;
                    miss += op.Miss;
                }
                double elapsed = lane.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
                L($"  {lane.Name,-22} {lane.Seed,12} {(lane.Reader ? "读" : "写"),4} {lane.OpsDone,8} {fail,8} {miss,8} {elapsed,10:N1} ms {lane.Wait.AvgMs,12:N2} ms {lane.All.AvgMs,8:N2} ms");
            }
            L();

            L("── 操作汇总 ────────────────────────────────────────────────────────");
            L($"  {"操作",-16} {"次数",10} {"失败",8} {"未命中",8} {"avg",10} {"min",10} {"max",10} {"ops/s",10}");
            foreach (var kv in Ops.OrderBy(x => x.Key.ToString()))
            {
                var a = kv.Value;
                double sec = a.Ticks * 1.0 / Stopwatch.Frequency;
                double qps = sec <= 0 ? 0 : a.Total / sec;
                L($"  {kv.Key,-16} {a.Total,10} {a.Fail,8} {a.Miss,8} {a.AvgMs,8:N2} ms {a.MinMs,8:N2} ms {a.MaxMs,8:N2} ms {qps,10:N1}");
            }
            L();

            L("── 延迟直方图（全操作合并）──────────────────────────────────────────");
            var merged = new int[10];
            long mergedN = 0;
            foreach (var a in Ops.Values)
            {
                for (int i = 0; i < 10; i++) merged[i] += a.Buckets[i];
                mergedN += a.Total;
            }
            int barMax = merged.Max();
            for (int i = 0; i < 10; i++)
            {
                int w = barMax == 0 ? 0 : merged[i] * 28 / barMax;
                double pct = mergedN == 0 ? 0 : merged[i] * 100.0 / mergedN;
                L($"  {Agg.BucketLabels[i],-12} {merged[i],8} {pct,6:N1}%  {new string('█', w)}");
            }
            L();

            L("── 最终库快照 ──────────────────────────────────────────────────────");
            L($"  用户 {audit.Users}    SKU {audit.Skus}    订单 {audit.Orders}    明细 {audit.Lines}");
            L($"  支付 {audit.Pays}    工单 {audit.Tickets}    优惠券 {audit.Coupons}    审计 {audit.Audits}");
            L($"  订单状态  下单={audit.StCreated} 已付={audit.StPaid} 已发={audit.StShipped} 完成={audit.StDone} 取消={audit.StCancel} 退货={audit.StReturn}");
            L($"  支付状态  成功={audit.PayOk} 失败={audit.PayFail} 退款={audit.PayRefund}");
            L($"  工单状态  开={audit.TkOpen} 处理={audit.TkDoing} 完={audit.TkDone} 关={audit.TkClosed}");
            L($"  优惠券    未用={audit.CouponOpen} 已核={audit.CouponUsed}");
            L($"  库存合计  当前 {audit.StockNow}    种子时 {audit.StockSeed}    占用(在途+已售未退) {audit.StockHeld}");
            L($"  库存守恒  {(audit.StockOk ? "通过" : "失败")}    期望当前={audit.StockExpect}    差额={audit.StockDelta}");
            L($"  负库存    {audit.NegativeStock} 行    明细对不上的订单 {audit.LineMismatch}");
            L($"  抽样已付订单缺支付单  {audit.PaidWithoutPay}");
            L($"  校验指纹  {audit.Fingerprint:X16}");
            L();

            if (!Errors.IsEmpty)
            {
                L("── 捕获的异常（最多 24 条）────────────────────────────────────────");
                foreach (var e in Errors)
                    L("  " + e);
                L();
            }

            L("── 业务解读（本次实例）────────────────────────────────────────────");
            L("  本模拟把 LumDb 当成云栈商城大促中台：东/西仓同时收单，闪购批量事务");
            L("  拉长写锁；清算网关把「已下单」推进到「已支付」；仓配发货/完成/退货会");
            L("  回补库存；商品组改价改描述（变长列）；客服工单与券体系穿插增删改；");
            L("  数仓/风控/分页浏览走只读事务，与写事务在引擎 RW 锁上真实交错。");
            L("════════════════════════════════════════════════════════════════════");
            return sb.ToString();
        }

        private static string FormatBytes(long n)
        {
            if (n < 0) return "n/a";
            if (n < 1024) return $"{n} B";
            if (n < 1024 * 1024) return $"{n / 1024.0:N1} KB";
            return $"{n / (1024.0 * 1024.0):N2} MB";
        }
    }

    internal sealed class AuditResult
    {
        public int Users, Skus, Orders, Lines, Pays, Tickets, Coupons, Audits;
        public int StCreated, StPaid, StShipped, StDone, StCancel, StReturn;
        public int PayOk, PayFail, PayRefund;
        public int TkOpen, TkDoing, TkDone, TkClosed;
        public int CouponOpen, CouponUsed;
        public long StockNow, StockSeed, StockHeld, StockExpect, StockDelta;
        public bool StockOk;
        public int NegativeStock, LineMismatch, PaidWithoutPay;
        public ulong Fingerprint;
    }

    internal sealed class SimConfig
    {
        public int Seed = 20260826;
        public int Ops = 280;
        public int Users = 96;
        public int Skus = 48;
        public int Warmup = 120;
        public bool Mem;
        public bool Keep;
        public bool Serial;
        public int TimeoutMs = 180_000;

        public int WriterOps => Ops;
        public int ReaderOps => (int)(Ops * 1.4);

        public static SimConfig Parse(string[] args)
        {
            var c = new SimConfig();
            foreach (var a in args)
            {
                if (TryNum(a, "--seed=", out int seed)) c.Seed = seed;
                else if (TryNum(a, "--ops=", out int ops)) c.Ops = Math.Max(20, ops);
                else if (TryNum(a, "--users=", out int u)) c.Users = Math.Clamp(u, 8, 5000);
                else if (TryNum(a, "--skus=", out int s)) c.Skus = Math.Clamp(s, 4, 2000);
                else if (TryNum(a, "--warmup=", out int w)) c.Warmup = Math.Max(0, w);
                else if (TryNum(a, "--timeout=", out int t)) c.TimeoutMs = Math.Max(1000, t);
                else if (a.Equals("--mem", StringComparison.OrdinalIgnoreCase)) c.Mem = true;
                else if (a.Equals("--keep", StringComparison.OrdinalIgnoreCase)) c.Keep = true;
                else if (a.Equals("--serial", StringComparison.OrdinalIgnoreCase)) c.Serial = true;
            }
            return c;
        }

        static bool TryNum(string arg, string prefix, out int v)
        {
            v = 0;
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            return int.TryParse(arg.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }
    }
}
