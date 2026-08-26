using System.Diagnostics;
using System.Text;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Structure;

namespace ConsoleTest.NexusMart
{
    /// <summary>
    /// 云栈商城运营中台实例：多种子车道、读写交错、增删查改混合负载，并输出分步计时报告。
    /// </summary>
    internal static class NexusMartSimulation
    {
        public static void Run(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Any(a => a is "-h" or "--help"))
            {
                PrintHelp();
                return;
            }

            var cfg = SimConfig.Parse(args);
            var stats = new SimStats();
            var dbPath = Path.Combine(Path.GetTempPath(), $"nexusmart-{cfg.Seed}-{Guid.NewGuid():N}.db");

            Console.WriteLine();
            Console.WriteLine("云栈商城 NexusMart 运营中台实例启动");
            Console.WriteLine($"  种子 {cfg.Seed}  用户 {cfg.Users}  SKU {cfg.Skus}  写ops/车道 {cfg.WriterOps}  读ops/车道 {cfg.ReaderOps}");
            if (cfg.Mem)
                Console.WriteLine(cfg.Keep ? $"  库 共享内存（结束将 dump 到 {dbPath}）" : "  库 共享内存（同一 DbEngine 跨事务共用）");
            else
                Console.WriteLine($"  库 {dbPath}");
            Console.WriteLine();

            using var eng = cfg.Mem ? new DbEngine() : new DbEngine(dbPath, true);
            eng.TimeoutMilliseconds = cfg.TimeoutMs;
            var ctx = new SimCtx(cfg, eng, stats);

            var total = Stopwatch.StartNew();
            Phase("1. 建表 / 索引", stats, () => Bootstrap(ctx));
            Phase("2. 主数据灌入", stats, () => SeedMaster(ctx));
            Phase("3. 预热下单（串行）", stats, () => Warmup(ctx));

            var stormWatch = Stopwatch.StartNew();
            Console.WriteLine("  4. 大促风暴 开始（多路随机并发）…");
            using var live = new CancellationTokenSource();
            var monitor = Task.Run(() => MonitorLoop(stats, stormWatch, live.Token));
            try
            {
                Storm(ctx);
            }
            finally
            {
                live.Cancel();
                try { monitor.Wait(2000); } catch { /* ignore */ }
            }
            stormWatch.Stop();
            stats.Phases.Add(("4. 大促风暴", stormWatch.Elapsed.TotalMilliseconds));
            Console.WriteLine($"  4. 大促风暴 完成  {stormWatch.Elapsed.TotalMilliseconds:N1} ms");

            AuditResult audit = default!;
            Phase("5. 收尾对账 / 快照", stats, () => audit = FinalAudit(ctx));
            total.Stop();
            stats.WallMs = total.Elapsed.TotalMilliseconds;

            long dbBytes = -1;
            if (cfg.Mem)
            {
                if (cfg.Keep)
                {
                    eng.SaveTo(dbPath);
                    dbBytes = new FileInfo(dbPath).Length;
                }
            }
            else
            {
                dbBytes = File.Exists(dbPath) ? new FileInfo(dbPath).Length : -1;
            }

            long memKb = Process.GetCurrentProcess().WorkingSet64 / 1024;
            var report = stats.BuildReport(cfg, audit, cfg.Mem ? (cfg.Keep ? dbPath : "(memory)") : dbPath, dbBytes, memKb);
            Console.WriteLine();
            Console.WriteLine(report);

            var reportPath = Path.ChangeExtension(dbPath, ".report.txt");
            File.WriteAllText(reportPath, report, Encoding.UTF8);
            Console.WriteLine($"报告已写入 {reportPath}");

            if (!cfg.Keep && !cfg.Mem)
                eng.SetDestoryOnDisposed();
        }

        static void PrintHelp()
        {
            Console.WriteLine("""
                云栈商城 NexusMart 运营中台实例
                  --seed=20260826     主随机种子（各车道派生，可复现动作序列）
                  --ops=280           每条写车道操作数（读车道为 1.4 倍）
                  --users=96          种子用户数
                  --skus=48           SKU 数
                  --warmup=120        风暴前串行预热订单数
                  --timeout=180000    事务等待超时毫秒
                  --keep              结束后保留 db 与报告文件；--mem 时将内存库 SaveTo 落盘
                  --serial            车道串行（同一种子下快照可对齐）
                  --mem               使用进程内共享内存库（同一 DbEngine 跨事务共用）
                """);
        }

        static void Phase(string name, SimStats stats, Action act)
        {
            Console.Write($"  {name} … ");
            var sw = Stopwatch.StartNew();
            act();
            sw.Stop();
            stats.Phases.Add((name, sw.Elapsed.TotalMilliseconds));
            Console.WriteLine($"{sw.Elapsed.TotalMilliseconds:N1} ms");
        }

        static async Task MonitorLoop(SimStats stats, Stopwatch sw, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(2500, ct); }
                catch (OperationCanceledException) { break; }
                Console.WriteLine($"     · 风暴 {sw.Elapsed.TotalSeconds,6:N1}s  累计操作 {stats.HeartbeatOps}");
            }
        }

        static void Bootstrap(SimCtx ctx)
        {
            using var ts = ctx.Eng.StartTransaction();
            Must(ts.Create<NxUser>(T.User));
            Must(ts.Create<NxSku>(T.Sku));
            Must(ts.Create<NxOrder>(T.Order));
            Must(ts.Create<NxLine>(T.Line));
            Must(ts.Create<NxPay>(T.Pay));
            Must(ts.Create<NxTicket>(T.Ticket));
            Must(ts.Create<NxCoupon>(T.Coupon));
            Must(ts.Create<NxAudit>(T.Audit));
        }

        static void SeedMaster(SimCtx ctx)
        {
            var rng = new Random(Mix(ctx.Cfg.Seed, 0));
            using (var ts = ctx.Eng.StartTransaction())
            {
                for (int i = 1; i <= ctx.Cfg.Users; i++)
                {
                    Must(ts.Insert(T.User, new NxUser
                    {
                        UserNo = i,
                        Login = $"u{i:D5}",
                        Region = (byte)(i % 7),
                        Level = 1 + i % 5,
                        Balance = 800m + i * 3.5m,
                        CreatedUtc = ctx.Utc(0, i),
                    }));
                }

                for (int s = 1; s <= ctx.Cfg.Skus; s++)
                {
                    int stock = 90 + rng.Next(0, 80);
                    ctx.SeedStock[s] = stock;
                    Must(ts.Insert(T.Sku, new NxSku
                    {
                        SkuCode = s,
                        Name = $"SKU-{s:D4}",
                        Category = (byte)(s % 11),
                        Price = 12.9m + s * 1.7m,
                        Stock = stock,
                        WarehouseId = 1 + s % 3,
                        Fingerprint = Fp(rng),
                        Desc = Lorem(rng, 24, 80),
                        UpdatedUtc = ctx.Utc(0, 10_000 + s),
                    }));
                }
            }

            using (var ts = ctx.Eng.StartTransaction())
            {
                Parallel.For(0, ctx.Cfg.Users, i =>
                {
                    int userNo = i + 1;
                    if (userNo % 3 != 0) return;
                    string code = $"S0{userNo:X5}";
                    ts.Insert(T.Coupon, new NxCoupon
                    {
                        Code = code,
                        UserNo = userNo,
                        Discount = 5m + (userNo % 7),
                        Used = false,
                        ExpireUtc = ctx.Utc(0, 50_000 + userNo).AddDays(30),
                    });
                });
            }
        }

        static void Warmup(SimCtx ctx)
        {
            var lane = ctx.Stats.Lane("Warmup", Mix(ctx.Cfg.Seed, 0), reader: false);
            var rng = new Random(Mix(ctx.Cfg.Seed, 0));
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < ctx.Cfg.Warmup; i++)
                TimedWrite(ctx, lane, OpKind.PlaceOrder, ts => PlaceOrder(ts, ctx, rng, laneId: 0, seq: i + 1));
            lane.ElapsedTicks = sw.ElapsedTicks;
        }

        static void Storm(SimCtx ctx)
        {
            var lanes = BuildLanes(ctx);
            if (ctx.Cfg.Serial)
            {
                foreach (var spec in lanes)
                    RunLane(ctx, spec);
                return;
            }

            var gate = new ManualResetEventSlim(false);
            var tasks = lanes.Select(spec => Task.Run(() =>
            {
                gate.Wait();
                RunLane(ctx, spec);
            })).ToArray();

            gate.Set();
            Task.WaitAll(tasks);
        }

        static LaneSpec[] BuildLanes(SimCtx ctx)
        {
            return
            [
                new(1, "Intake-East", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 1)),
                new(2, "Intake-West", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 2)),
                new(3, "FlashSale", false, Math.Max(40, ctx.Cfg.WriterOps / 2), Mix(ctx.Cfg.Seed, 3)),
                new(4, "PaymentClear", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 4)),
                new(5, "Fulfillment", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 5)),
                new(6, "Catalog", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 6)),
                new(7, "BackOffice", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 7)),
                new(8, "SupportDesk", false, ctx.Cfg.WriterOps, Mix(ctx.Cfg.Seed, 8)),
                new(9, "BiRealtime", true, ctx.Cfg.ReaderOps, Mix(ctx.Cfg.Seed, 9)),
                new(10, "SearchDesk", true, ctx.Cfg.ReaderOps, Mix(ctx.Cfg.Seed, 10)),
                new(11, "RiskControl", true, ctx.Cfg.ReaderOps, Mix(ctx.Cfg.Seed, 11)),
                new(12, "OpsPager", true, ctx.Cfg.ReaderOps, Mix(ctx.Cfg.Seed, 12)),
                new(13, "Heartbeat", true, ctx.Cfg.ReaderOps, Mix(ctx.Cfg.Seed, 13)),
            ];
        }

        static void RunLane(SimCtx ctx, LaneSpec spec)
        {
            var lane = ctx.Stats.Lane(spec.Name, spec.Seed, spec.Reader);
            var rng = new Random(spec.Seed);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < spec.Ops; i++)
            {
                try
                {
                    if (spec.Reader) ReaderStep(ctx, lane, spec, rng, i);
                    else WriterStep(ctx, lane, spec, rng, i);
                }
                catch (Exception ex)
                {
                    ctx.Stats.Error(spec.Name, ex);
                    ctx.Stats.Record(lane, OpKind.Other, 0, false);
                }
            }
            sw.Stop();
            lane.ElapsedTicks = sw.ElapsedTicks;
        }

        static void WriterStep(SimCtx ctx, LaneAgg lane, LaneSpec spec, Random rng, int i)
        {
            switch (spec.LaneId)
            {
                case 1:
                case 2:
                    switch (Pick(rng, [72, 14, 8, 6]))
                    {
                        case 0: TimedWrite(ctx, lane, OpKind.PlaceOrder, ts => PlaceOrder(ts, ctx, rng, spec.LaneId, i + 1)); break;
                        case 1: TimedWrite(ctx, lane, OpKind.Find, ts => FindOrder(ts, rng, spec.LaneId)); break;
                        case 2: TimedWrite(ctx, lane, OpKind.Find, ts => FindSku(ts, rng, ctx.Cfg.Skus)); break;
                        default: TimedWrite(ctx, lane, OpKind.Count, ts => CountCreated(ts)); break;
                    }
                    break;
                case 3:
                    TimedWrite(ctx, lane, OpKind.Batch, ts => FlashBatch(ts, ctx, rng, spec.LaneId, i));
                    break;
                case 4:
                    TimedWrite(ctx, lane, OpKind.Pay, ts => PayCreated(ts, ctx, rng, spec.LaneId, i + 1));
                    break;
                case 5:
                    switch (Pick(rng, [50, 28, 22]))
                    {
                        case 0: TimedWrite(ctx, lane, OpKind.Ship, ts => ShipPaid(ts, ctx, rng, spec.LaneId, i + 1)); break;
                        case 1: TimedWrite(ctx, lane, OpKind.Complete, ts => CompleteShipped(ts, rng)); break;
                        default: TimedWrite(ctx, lane, OpKind.Return, ts => ReturnOne(ts, ctx, rng, spec.LaneId, i + 1)); break;
                    }
                    break;
                case 6:
                    TimedWrite(ctx, lane, OpKind.Catalog, ts => CatalogEdit(ts, ctx, rng, spec.LaneId, i + 1));
                    break;
                case 7:
                    switch (Pick(rng, [16, 14, 12, 14, 12, 10, 10, 12]))
                    {
                        case 0: TimedWrite(ctx, lane, OpKind.TicketNew, ts => OpenTicket(ts, ctx, rng, spec.LaneId, i + 1)); break;
                        case 1: TimedWrite(ctx, lane, OpKind.CouponIssue, ts => IssueCoupon(ts, ctx, rng, spec.LaneId, i + 1)); break;
                        case 2: TimedWrite(ctx, lane, OpKind.CouponUse, ts => RedeemCoupon(ts, rng)); break;
                        case 3: TimedWrite(ctx, lane, OpKind.Cancel, ts => CancelUnpaid(ts, rng)); break;
                        case 4: TimedWrite(ctx, lane, OpKind.UserAdd, ts => RegisterUser(ts, ctx, spec.LaneId, i + 1)); break;
                        case 5: TimedWrite(ctx, lane, OpKind.Audit, ts => WriteAudit(ts, ctx, spec.LaneId, i + 1, 700, 0, "backoffice")); break;
                        case 6: TimedWrite(ctx, lane, OpKind.FindMany, ts => PageOrders(ts, rng)); break;
                        default: TimedWrite(ctx, lane, OpKind.TicketDel, ts => SweepClosedTickets(ts, rng)); break;
                    }
                    break;
                default:
                    switch (Pick(rng, [40, 30, 18, 12]))
                    {
                        case 0: TimedWrite(ctx, lane, OpKind.TicketNew, ts => OpenTicket(ts, ctx, rng, spec.LaneId, i + 1)); break;
                        case 1: TimedWrite(ctx, lane, OpKind.TicketUpd, ts => TouchTicket(ts, rng)); break;
                        case 2: TimedWrite(ctx, lane, OpKind.Find, ts => FindOrder(ts, rng, spec.LaneId)); break;
                        default: TimedWrite(ctx, lane, OpKind.TicketDel, ts => SweepClosedTickets(ts, rng)); break;
                    }
                    break;
            }
        }

        static void ReaderStep(SimCtx ctx, LaneAgg lane, LaneSpec spec, Random rng, int i)
        {
            switch (spec.LaneId)
            {
                case 9:
                    switch (Pick(rng, [40, 35, 25]))
                    {
                        case 0: TimedRead(ctx, lane, OpKind.Count, ts => CountByStatus(ts, rng)); break;
                        case 1: TimedRead(ctx, lane, OpKind.FindMany, ts => PageOrdersRo(ts, rng)); break;
                        default: TimedRead(ctx, lane, OpKind.Scan, ts => ScanSkuStock(ts)); break;
                    }
                    break;
                case 10:
                    switch (Pick(rng, [55, 25, 20]))
                    {
                        case 0: TimedRead(ctx, lane, OpKind.Find, ts => LookupSkuRo(ts, rng, ctx.Cfg.Skus)); break;
                        case 1: TimedRead(ctx, lane, OpKind.Find, ts => LookupUserRo(ts, rng, ctx)); break;
                        default: TimedRead(ctx, lane, OpKind.FindMany, ts => HotLines(ts, rng, ctx.Cfg.Skus)); break;
                    }
                    break;
                case 11:
                    TimedRead(ctx, lane, OpKind.FindMany, ts => RiskSlice(ts, rng));
                    break;
                case 12:
                    TimedRead(ctx, lane, OpKind.FindMany, ts => PagedBrowse(ts, rng));
                    break;
                default:
                    TimedRead(ctx, lane, OpKind.Scan, ts => HeartbeatScan(ts, rng));
                    break;
            }
        }

        // ── writers ──────────────────────────────────────────────────────────

        static Outcome PlaceOrder(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            int live = ctx.LiveUsers;
            int userNo = rng.Next(1, live + 1);
            if (userNo > ctx.Cfg.Users && userNo < 10_000)
                userNo = 1 + rng.Next(ctx.Cfg.Users);

            var user = ts.FindEntity<NxUser>(T.User, "UserNo", userNo);
            if (!user.IsSuccess) return Outcome.Missed;

            int want = rng.Next(1, 4);
            var cart = new List<(int sku, int qty, decimal price, int wh, int newStock)>(want);
            var seen = new HashSet<int>();
            for (int k = 0; k < want + 4 && cart.Count < want; k++)
            {
                int sku = 1 + rng.Next(ctx.Cfg.Skus);
                if (!seen.Add(sku)) continue;
                var row = ts.FindEntity<NxSku>(T.Sku, "SkuCode", sku);
                if (!row.IsSuccess) continue;
                int qty = 1 + rng.Next(3);
                if (row.Value.Stock < qty) continue;
                cart.Add((sku, qty, row.Value.Price, row.Value.WarehouseId, row.Value.Stock - qty));
            }
            if (cart.Count == 0) return Outcome.Missed;

            int orderNo = laneId * 1_000_000 + seq;
            decimal amount = 0;
            foreach (var c in cart) amount += c.price * c.qty;
            var now = ctx.Utc(laneId, seq);
            var ins = ts.Insert(T.Order, new NxOrder
            {
                OrderNo = orderNo,
                UserNo = userNo,
                Status = OrderSt.Created,
                PayAmount = amount,
                ItemCount = cart.Count,
                WarehouseId = cart[0].wh,
                CreatedUtc = now,
                Note = rng.Next(5) == 0 ? Lorem(rng, 40, 220) : $"lane{laneId}/seq{seq}",
            });
            if (!ins.IsSuccess) return Outcome.Fail;

            for (int i = 0; i < cart.Count; i++)
            {
                var c = cart[i];
                if (!ts.Insert(T.Line, new NxLine
                {
                    LineNo = orderNo * 8 + i,
                    OrderNo = orderNo,
                    SkuCode = c.sku,
                    Qty = c.qty,
                    UnitPrice = c.price,
                }).IsSuccess) return Outcome.Fail;

                if (!ts.Update(T.Sku, "SkuCode", c.sku, "Stock", c.newStock).IsSuccess)
                    return Outcome.Fail;
            }

            WriteAudit(ts, ctx, laneId, seq, 100, orderNo, $"place u{userNo} n{cart.Count} {amount:0.00}");
            return Outcome.Hit;
        }

        static Outcome FlashBatch(ITransaction ts, SimCtx ctx, Random rng, int laneId, int round)
        {
            int n = 2 + rng.Next(4);
            int ok = 0;
            for (int k = 0; k < n; k++)
            {
                int seq = round * 8 + k + 1;
                var r = PlaceOrder(ts, ctx, rng, laneId, seq);
                if (!r.Ok) return Outcome.Fail;
                if (!r.Miss) ok++;
            }
            if (ok == 0) return Outcome.Missed;
            WriteAudit(ts, ctx, laneId, round, 101, ok, $"flash-batch {ok}/{n}");
            return Outcome.Hit;
        }

        static Outcome PayCreated(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            var found = ts.Find<NxOrder>(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) == OrderSt.Created, 0, 16);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;

            int paid = 0;
            int cap = 1 + rng.Next(3);
            foreach (var o in found.Values)
            {
                if (paid >= cap) break;
                o.Status = OrderSt.Paid;
                if (!ts.UpdateEntity(T.Order, o.Id, o).IsSuccess) continue;

                int payNo = 20_000_000 + Interlocked.Increment(ref ctx.PayClock);
                bool fail = rng.Next(12) == 0;
                if (!ts.Insert(T.Pay, new NxPay
                {
                    PayNo = payNo,
                    OrderNo = o.OrderNo,
                    Channel = (byte)(1 + rng.Next(4)),
                    Amount = o.PayAmount,
                    Status = fail ? PaySt.Fail : PaySt.Ok,
                    PaidUtc = ctx.Utc(laneId, seq),
                }).IsSuccess) return Outcome.Fail;

                if (fail)
                {
                    o.Status = OrderSt.Created;
                    ts.UpdateEntity(T.Order, o.Id, o);
                    continue;
                }

                var user = ts.FindEntity<NxUser>(T.User, "UserNo", o.UserNo);
                if (user.IsSuccess && user.Value.Balance >= o.PayAmount)
                {
                    user.Value.Balance -= o.PayAmount;
                    ts.UpdateEntity(T.User, user.Value.Id, user.Value);
                }
                paid++;
            }
            return paid == 0 ? Outcome.Missed : Outcome.Hit;
        }

        static Outcome ShipPaid(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            var found = ts.Find<NxOrder>(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) == OrderSt.Paid, 0, 12);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            var o = found.Values[rng.Next(found.Values.Count)];
            o.Status = OrderSt.Shipped;
            o.Note = (o.Note ?? "") + "/ship";
            if (o.Note.Length > 400) o.Note = o.Note[^360..];
            if (!ts.UpdateEntity(T.Order, o.Id, o).IsSuccess) return Outcome.Fail;
            WriteAudit(ts, ctx, laneId, seq, 200, o.OrderNo, "ship");
            return Outcome.Hit;
        }

        static Outcome CompleteShipped(ITransaction ts, Random rng)
        {
            var found = ts.Find<NxOrder>(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) == OrderSt.Shipped, 0, 12);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            var o = found.Values[rng.Next(found.Values.Count)];
            o.Status = OrderSt.Completed;
            if (!ts.UpdateEntity(T.Order, o.Id, o).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome ReturnOne(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            var found = ts.Find<NxOrder>(T.Order, (ref RowView r) =>
            {
                int st = NxOrder.GetStatus(ref r);
                return st == OrderSt.Shipped || st == OrderSt.Completed;
            }, 0, 10);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            var o = found.Values[rng.Next(found.Values.Count)];
            if (!Restock(ts, o.OrderNo, o.ItemCount)) return Outcome.Fail;
            o.Status = OrderSt.Returned;
            if (!ts.UpdateEntity(T.Order, o.Id, o).IsSuccess) return Outcome.Fail;
            ts.Insert(T.Pay, new NxPay
            {
                PayNo = 40_000_000 + Interlocked.Increment(ref ctx.PayClock),
                OrderNo = o.OrderNo,
                Channel = 9,
                Amount = o.PayAmount,
                Status = PaySt.Refund,
                PaidUtc = ctx.Utc(laneId, seq),
            });
            WriteAudit(ts, ctx, laneId, seq, 250, o.OrderNo, "return");
            return Outcome.Hit;
        }

        static Outcome CancelUnpaid(ITransaction ts, Random rng)
        {
            var found = ts.Find<NxOrder>(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) == OrderSt.Created, 0, 10);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            var o = found.Values[rng.Next(found.Values.Count)];
            if (!Restock(ts, o.OrderNo, o.ItemCount)) return Outcome.Fail;
            o.Status = OrderSt.Cancelled;
            if (!ts.UpdateEntity(T.Order, o.Id, o).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome CatalogEdit(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            int sku = 1 + rng.Next(ctx.Cfg.Skus);
            var row = ts.FindEntity<NxSku>(T.Sku, "SkuCode", sku);
            if (!row.IsSuccess) return Outcome.Missed;
            var v = row.Value;
            var delta = (decimal)(rng.NextDouble() * 0.08 - 0.03);
            v.Price = decimal.Round(v.Price * (1 + delta), 2);
            if (v.Price < 1) v.Price = 1;
            v.Desc = Lorem(rng, 16, rng.Next(8) == 0 ? 1200 : 180);
            v.Fingerprint = Fp(rng);
            v.UpdatedUtc = ctx.Utc(laneId, seq);
            if (rng.Next(4) == 0)
                v.Name = $"SKU-{sku:D4}-H";
            if (!ts.UpdateEntity(T.Sku, v.Id, v).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome OpenTicket(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            int ticketNo = 30_000_000 + laneId * 100_000 + seq;
            int userNo = 1 + rng.Next(ctx.Cfg.Users);
            int orderNo = rng.Next(3) == 0 ? 0 : (1 + rng.Next(3)) * 1_000_000 + 1 + rng.Next(Math.Max(1, seq));
            if (!ts.Insert(T.Ticket, new NxTicket
            {
                TicketNo = ticketNo,
                UserNo = userNo,
                OrderNo = orderNo,
                Status = TicketSt.Open,
                Priority = (byte)(1 + rng.Next(3)),
                Body = Lorem(rng, 40, 400),
                UpdatedUtc = ctx.Utc(laneId, seq),
            }).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome TouchTicket(ITransaction ts, Random rng)
        {
            var found = ts.Find<NxTicket>(T.Ticket, (ref RowView r) =>
            {
                int st = NxTicket.GetStatus(ref r);
                return st == TicketSt.Open || st == TicketSt.Doing;
            }, 0, 8);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            var t = found.Values[rng.Next(found.Values.Count)];
            t.Status = t.Status == TicketSt.Open ? TicketSt.Doing : (rng.Next(3) == 0 ? TicketSt.Closed : TicketSt.Done);
            t.Body += $" /t{rng.Next(1000)}";
            t.UpdatedUtc = DateTime.SpecifyKind(t.UpdatedUtc.AddSeconds(30), DateTimeKind.Utc);
            if (!ts.UpdateEntity(T.Ticket, t.Id, t).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome SweepClosedTickets(ITransaction ts, Random rng)
        {
            var found = ts.Find<NxTicket>(T.Ticket, (ref RowView r) =>
            {
                int st = NxTicket.GetStatus(ref r);
                return st == TicketSt.Closed || st == TicketSt.Done;
            }, 0, 6);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            int n = 0;
            foreach (var t in found.Values)
            {
                if (rng.Next(3) == 0) continue;
                if (ts.Delete(T.Ticket, t.Id).IsSuccess) n++;
            }
            return n == 0 ? Outcome.Missed : Outcome.Hit;
        }

        static Outcome IssueCoupon(ITransaction ts, SimCtx ctx, Random rng, int laneId, int seq)
        {
            string code = $"L{laneId:X}{seq:X5}";
            if (code.Length > 16) code = code[..16];
            int userNo = 1 + rng.Next(ctx.Cfg.Users);
            if (!ts.Insert(T.Coupon, new NxCoupon
            {
                Code = code,
                UserNo = userNo,
                Discount = 3m + rng.Next(20),
                Used = false,
                ExpireUtc = ctx.Utc(laneId, seq).AddDays(14),
            }).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome RedeemCoupon(ITransaction ts, Random rng)
        {
            var found = ts.Find<NxCoupon>(T.Coupon, (ref RowView r) => !NxCoupon.GetUsed(ref r), 0, 10);
            if (!found.IsSuccess || found.Values.Count == 0) return Outcome.Missed;
            var c = found.Values[rng.Next(found.Values.Count)];
            c.Used = true;
            if (!ts.UpdateEntity(T.Coupon, c.Id, c).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome RegisterUser(ITransaction ts, SimCtx ctx, int laneId, int seq)
        {
            int no = 10_000 + Interlocked.Increment(ref ctx.ExtraUsers);
            if (!ts.Insert(T.User, new NxUser
            {
                UserNo = no,
                Login = no < 100_000 ? $"u{no:D5}" : $"u{no}",
                Region = (byte)(no % 7),
                Level = 1,
                Balance = 200m,
                CreatedUtc = ctx.Utc(laneId, seq),
            }).IsSuccess) return Outcome.Fail;
            return Outcome.Hit;
        }

        static Outcome WriteAudit(ITransaction ts, SimCtx ctx, int laneId, int seq, int action, int target, string detail)
        {
            long auditSeq = ((long)laneId << 40) ^ ((long)action << 20) ^ (uint)seq ^ ((long)Interlocked.Increment(ref ctx.AuditClock) << 8);
            var r = ts.Insert(T.Audit, new NxAudit
            {
                Seq = auditSeq,
                Actor = laneId,
                Action = action,
                Target = target,
                Detail = detail,
                AtUtc = ctx.Utc(laneId, seq),
            });
            return r.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome FindOrder(ITransaction ts, Random rng, int laneId)
        {
            int orderNo = laneId * 1_000_000 + 1 + rng.Next(800);
            var r = ts.FindEntity<NxOrder>(T.Order, "OrderNo", orderNo);
            return r.IsSuccess ? Outcome.Hit : Outcome.Missed;
        }

        static Outcome FindSku(ITransaction ts, Random rng, int skus)
        {
            var r = ts.FindEntity<NxSku>(T.Sku, "SkuCode", 1 + rng.Next(skus));
            return r.IsSuccess ? Outcome.Hit : Outcome.Missed;
        }

        static Outcome CountCreated(ITransaction ts)
        {
            var c = ts.Count(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) == OrderSt.Created);
            return c.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome PageOrders(ITransaction ts, Random rng)
        {
            uint skip = (uint)rng.Next(0, 40);
            var r = ts.Find<NxOrder>(T.Order, (ref RowView r) => true, skip, 12);
            return r.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static bool Restock(ITransaction ts, int orderNo, int itemCount)
        {
            int n = Math.Max(itemCount, 1);
            for (int i = 0; i < n && i < 8; i++)
            {
                var line = ts.FindEntity<NxLine>(T.Line, "LineNo", orderNo * 8 + i);
                if (!line.IsSuccess) continue;
                var sku = ts.FindEntity<NxSku>(T.Sku, "SkuCode", line.Value.SkuCode);
                if (!sku.IsSuccess) return false;
                if (!ts.Update(T.Sku, "SkuCode", sku.Value.SkuCode, "Stock", sku.Value.Stock + line.Value.Qty).IsSuccess)
                    return false;
            }
            return true;
        }

        // ── readers ──────────────────────────────────────────────────────────

        static Outcome CountByStatus(ITransactionReadonly ts, Random rng)
        {
            int st = rng.Next(6);
            var c = ts.Count(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) == st);
            return c.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome PageOrdersRo(ITransactionReadonly ts, Random rng)
        {
            uint skip = (uint)rng.Next(0, 30);
            var r = ts.Find<NxOrder>(T.Order, (ref RowView r) => NxOrder.GetStatus(ref r) >= 0, skip, 10);
            return r.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome ScanSkuStock(ITransactionReadonly ts)
        {
            int n = 0;
            long stock = 0;
            ts.GoThrough(T.Sku, (ref RowView r) =>
            {
                stock += NxSku.GetStock(ref r);
                return ++n < 4000;
            });
            return n > 0 ? Outcome.Hit : Outcome.Missed;
        }

        static Outcome LookupSkuRo(ITransactionReadonly ts, Random rng, int skus)
        {
            var r = ts.FindEntity<NxSku>(T.Sku, "SkuCode", 1 + rng.Next(skus));
            return r.IsSuccess ? Outcome.Hit : Outcome.Missed;
        }

        static Outcome LookupUserRo(ITransactionReadonly ts, Random rng, SimCtx ctx)
        {
            int no = 1 + rng.Next(ctx.LiveUsers);
            if (no > ctx.Cfg.Users && no < 10_000) no = 1 + rng.Next(ctx.Cfg.Users);
            var r = ts.FindEntity<NxUser>(T.User, "UserNo", no);
            return r.IsSuccess ? Outcome.Hit : Outcome.Missed;
        }

        static Outcome HotLines(ITransactionReadonly ts, Random rng, int skus)
        {
            int sku = 1 + rng.Next(skus);
            var r = ts.Find<NxLine>(T.Line, (ref RowView row) => NxLine.GetSkuCode(ref row) == sku, 0, 20);
            return r.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome RiskSlice(ITransactionReadonly ts, Random rng)
        {
            decimal floor = 40m + rng.Next(80);
            var r = ts.Find<NxOrder>(T.Order, (ref RowView row) => NxOrder.GetPayAmount(ref row) >= floor, 0, 25);
            return r.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome PagedBrowse(ITransactionReadonly ts, Random rng)
        {
            uint skip = (uint)rng.Next(0, 80);
            uint limit = (uint)(8 + rng.Next(16));
            var a = ts.Find<NxOrder>(T.Order, (ref RowView r) => true, skip, limit);
            var b = ts.Find<NxTicket>(T.Ticket, (ref RowView r) => NxTicket.GetPriority(ref r) >= 2, 0, 8);
            return a.IsSuccess && b.IsSuccess ? Outcome.Hit : Outcome.Fail;
        }

        static Outcome HeartbeatScan(ITransactionReadonly ts, Random rng)
        {
            int cap = 200 + rng.Next(800);
            int n = 0;
            if (rng.Next(2) == 0)
            {
                ts.GoThrough(T.User, (ref RowView r) => ++n < cap);
            }
            else
            {
                ts.GoThrough(T.Audit, (ref RowView r) => ++n < cap);
            }
            return Outcome.Hit;
        }

        // ── audit ────────────────────────────────────────────────────────────

        static AuditResult FinalAudit(SimCtx ctx)
        {
            var a = new AuditResult { StockSeed = ctx.SeedStock.Skip(1).Sum() };
            var orderSt = new Dictionary<int, int>();
            var orderItems = new Dictionary<int, int>();
            var paidNos = new HashSet<int>();
            var payNos = new HashSet<int>();

            using var ts = ctx.Eng.StartTransactionReadonly();

            a.Users = (int)ts.Count(T.User, (ref RowView r) => true).Value;
            a.Skus = (int)ts.Count(T.Sku, (ref RowView r) => true).Value;
            a.Orders = (int)ts.Count(T.Order, (ref RowView r) => true).Value;
            a.Lines = (int)ts.Count(T.Line, (ref RowView r) => true).Value;
            a.Pays = (int)ts.Count(T.Pay, (ref RowView r) => true).Value;
            a.Tickets = (int)ts.Count(T.Ticket, (ref RowView r) => true).Value;
            a.Coupons = (int)ts.Count(T.Coupon, (ref RowView r) => true).Value;
            a.Audits = (int)ts.Count(T.Audit, (ref RowView r) => true).Value;

            ts.GoThrough(T.Order, (ref RowView r) =>
            {
                int no = NxOrder.GetOrderNo(ref r);
                int st = NxOrder.GetStatus(ref r);
                int items = NxOrder.GetItemCount(ref r);
                orderSt[no] = st;
                orderItems[no] = items;
                switch (st)
                {
                    case OrderSt.Created: a.StCreated++; break;
                    case OrderSt.Paid: a.StPaid++; paidNos.Add(no); break;
                    case OrderSt.Shipped: a.StShipped++; paidNos.Add(no); break;
                    case OrderSt.Completed: a.StDone++; paidNos.Add(no); break;
                    case OrderSt.Cancelled: a.StCancel++; break;
                    case OrderSt.Returned: a.StReturn++; break;
                }
                a.Fingerprint = Mix64(a.Fingerprint, (uint)no, (uint)st);
                return true;
            });

            var lineCount = new Dictionary<int, int>();
            ts.GoThrough(T.Line, (ref RowView r) =>
            {
                int no = NxLine.GetOrderNo(ref r);
                int qty = NxLine.GetQty(ref r);
                int sku = NxLine.GetSkuCode(ref r);
                lineCount[no] = lineCount.GetValueOrDefault(no) + 1;
                if (orderSt.TryGetValue(no, out int st) && st is OrderSt.Created or OrderSt.Paid or OrderSt.Shipped or OrderSt.Completed)
                    a.StockHeld += qty;
                a.Fingerprint = Mix64(a.Fingerprint, (uint)no, (uint)(sku * 31 + qty));
                return true;
            });

            foreach (var kv in orderItems)
            {
                int got = lineCount.GetValueOrDefault(kv.Key);
                if (got != kv.Value) a.LineMismatch++;
            }

            ts.GoThrough(T.Sku, (ref RowView r) =>
            {
                int stock = NxSku.GetStock(ref r);
                a.StockNow += stock;
                if (stock < 0) a.NegativeStock++;
                a.Fingerprint = Mix64(a.Fingerprint, (uint)NxSku.GetSkuCode(ref r), (uint)stock);
                return true;
            });

            ts.GoThrough(T.Pay, (ref RowView r) =>
            {
                int st = NxPay.GetStatus(ref r);
                int ono = NxPay.GetOrderNo(ref r);
                if (st == PaySt.Ok) { a.PayOk++; payNos.Add(ono); }
                else if (st == PaySt.Fail) a.PayFail++;
                else if (st == PaySt.Refund) a.PayRefund++;
                return true;
            });

            foreach (var no in paidNos)
                if (!payNos.Contains(no)) a.PaidWithoutPay++;

            ts.GoThrough(T.Ticket, (ref RowView r) =>
            {
                switch (NxTicket.GetStatus(ref r))
                {
                    case TicketSt.Open: a.TkOpen++; break;
                    case TicketSt.Doing: a.TkDoing++; break;
                    case TicketSt.Done: a.TkDone++; break;
                    case TicketSt.Closed: a.TkClosed++; break;
                }
                return true;
            });

            ts.GoThrough(T.Coupon, (ref RowView r) =>
            {
                if (NxCoupon.GetUsed(ref r)) a.CouponUsed++;
                else a.CouponOpen++;
                return true;
            });

            a.StockExpect = a.StockSeed - a.StockHeld;
            a.StockDelta = a.StockNow - a.StockExpect;
            a.StockOk = a.StockDelta == 0 && a.NegativeStock == 0;
            a.Fingerprint = Mix64(a.Fingerprint, (uint)a.Users, (uint)a.Orders);
            return a;
        }

        // ── plumbing ─────────────────────────────────────────────────────────

        static void TimedWrite(SimCtx ctx, LaneAgg lane, OpKind kind, Func<ITransaction, Outcome> body)
        {
            var w0 = Stopwatch.GetTimestamp();
            ITransaction? ts = null;
            try
            {
                ts = ctx.Eng.StartTransaction();
                ctx.Stats.Wait(lane, Stopwatch.GetTimestamp() - w0);
                var t0 = Stopwatch.GetTimestamp();
                var r = body(ts);
                if (!r.Ok) { try { ts.Discard(); } catch { /* already reset */ } }
                ctx.Stats.Record(lane, kind, Stopwatch.GetTimestamp() - t0, r.Ok, r.Miss);
            }
            catch (Exception ex)
            {
                ctx.Stats.Error(lane.Name, ex);
                ctx.Stats.Record(lane, kind, Stopwatch.GetTimestamp() - w0, false);
                try { ts?.Discard(); } catch { /* ignore */ }
            }
            finally
            {
                try { ts?.Dispose(); }
                catch (Exception ex) { ctx.Stats.Error(lane.Name, ex); }
            }
        }

        static void TimedRead(SimCtx ctx, LaneAgg lane, OpKind kind, Func<ITransactionReadonly, Outcome> body)
        {
            var w0 = Stopwatch.GetTimestamp();
            ITransactionReadonly? ts = null;
            try
            {
                ts = ctx.Eng.StartTransactionReadonly();
                ctx.Stats.Wait(lane, Stopwatch.GetTimestamp() - w0);
                var t0 = Stopwatch.GetTimestamp();
                var r = body(ts);
                ctx.Stats.Record(lane, kind, Stopwatch.GetTimestamp() - t0, r.Ok, r.Miss);
            }
            catch (Exception ex)
            {
                ctx.Stats.Error(lane.Name, ex);
                ctx.Stats.Record(lane, kind, Stopwatch.GetTimestamp() - w0, false);
            }
            finally
            {
                try { ts?.Dispose(); }
                catch (Exception ex) { ctx.Stats.Error(lane.Name, ex); }
            }
        }

        static void Must(LumDbEngine.Element.Engine.Results.IDbResult r)
        {
            if (!r.IsSuccess)
                throw new InvalidOperationException(r.Exception?.Message ?? "db op failed");
        }

        static int Pick(Random rng, int[] weights)
        {
            int sum = 0;
            foreach (var w in weights) sum += w;
            int x = rng.Next(sum);
            for (int i = 0; i < weights.Length; i++)
            {
                x -= weights[i];
                if (x < 0) return i;
            }
            return weights.Length - 1;
        }

        static int Mix(int seed, int lane)
        {
            unchecked
            {
                uint x = (uint)seed ^ ((uint)(lane + 1) * 0x9E3779B9u);
                x *= 16777619u;
                x ^= x >> 16;
                return (int)x;
            }
        }

        static ulong Mix64(ulong h, uint a, uint b)
        {
            unchecked
            {
                h ^= a;
                h *= 1099511628211UL;
                h ^= b;
                h *= 1099511628211UL;
                return h;
            }
        }

        static byte[] Fp(Random rng)
        {
            var b = new byte[8];
            rng.NextBytes(b);
            return b;
        }

        static string Lorem(Random rng, int min, int max)
        {
            int n = min + rng.Next(Math.Max(1, max - min + 1));
            Span<char> buf = n <= 256 ? stackalloc char[n] : new char[n];
            const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789 ";
            for (int i = 0; i < n; i++)
                buf[i] = alphabet[rng.Next(alphabet.Length)];
            return new string(buf);
        }

        readonly record struct LaneSpec(int LaneId, string Name, bool Reader, int Ops, int Seed);

        readonly struct Outcome
        {
            public bool Ok { get; init; }
            public bool Miss { get; init; }
            public static Outcome Hit => new() { Ok = true };
            public static Outcome Missed => new() { Ok = true, Miss = true };
            public static Outcome Fail => new() { Ok = false };
        }

        sealed class SimCtx
        {
            public SimCtx(SimConfig cfg, DbEngine eng, SimStats stats)
            {
                Cfg = cfg;
                Eng = eng;
                Stats = stats;
                SeedStock = new int[cfg.Skus + 1];
            }

            public SimConfig Cfg { get; }
            public DbEngine Eng { get; }
            public SimStats Stats { get; }
            public int[] SeedStock { get; }
            public int ExtraUsers;
            public int AuditClock;
            public int PayClock;
            public int LiveUsers => Cfg.Users + Volatile.Read(ref ExtraUsers);
            public DateTime Epoch { get; } = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            public DateTime Utc(int lane, int seq) => Epoch.AddSeconds(lane * 2_000_000L + seq);
        }
    }
}
