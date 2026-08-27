using System.Diagnostics;
using System.Globalization;
using System.Text;
using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Diagnostics;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;

namespace ConsoleTest.EditChurn
{
    /// <summary>
    /// 长期编辑 / 删改模拟：以 Update、Delete、Replace 为核心，跟踪文件大小与空位复用。
    /// </summary>
    internal static class EditChurnSimulation
    {
        public static void Run(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var cfg = ChurnConfig.Parse(args);
            var dbPath = Path.Combine(Path.GetTempPath(), $"lumdb-churn-{cfg.Seed}-{Guid.NewGuid():N}.db");
            var reportPath = Path.ChangeExtension(dbPath, ".report.txt");
            var checkpoints = new List<Checkpoint>();
            var rng = new Random(cfg.Seed);

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  LumDb 长期编辑 / 删改模拟（CRUD 以 U+D 为核心）");
            Console.WriteLine($"  种子 {cfg.Seed}  文档 {cfg.Docs}  轮次 {cfg.Rounds}  库 {dbPath}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            using var eng = new DbEngine(dbPath, true);
            eng.TimeoutMilliseconds = cfg.TimeoutMs;

            var total = Stopwatch.StartNew();

            Phase("1. 建表", () => Bootstrap(eng));
            Phase("2. 初稿灌入（长 StrVar）", () => SeedLongForm(eng, dbPath, cfg, rng, checkpoints));

            int nextDocId = cfg.Docs + 1;
            int nextCommentId = cfg.Docs * cfg.CommentsPerDoc + 1;
            var liveDocIds = Enumerable.Range(1, cfg.Docs).ToList();

            Phase($"3. 长期编辑 {cfg.Rounds} 轮", () =>
            {
                for (int round = 1; round <= cfg.Rounds; round++)
                {
                    using var ts = eng.StartTransaction();
                    var op = PickOp(rng, round);
                    switch (op)
                    {
                        case ChurnOp.UpdateShrink:
                            UpdateBodies(ts, rng, liveDocIds, shrink: true);
                            break;
                        case ChurnOp.UpdateGrow:
                            UpdateBodies(ts, rng, liveDocIds, shrink: false);
                            break;
                        case ChurnOp.UpdateTitle:
                            UpdateTitles(ts, rng, liveDocIds);
                            break;
                        case ChurnOp.DeleteDoc:
                            nextDocId = HardDeleteDocs(ts, rng, liveDocIds, nextDocId, cfg, rng);
                            break;
                        case ChurnOp.ReplaceDoc:
                            ReplaceDoc(ts, rng, liveDocIds, ref nextDocId, cfg, rng);
                            break;
                        case ChurnOp.CommentChurn:
                            CommentChurn(ts, rng, liveDocIds, ref nextCommentId, cfg);
                            break;
                        case ChurnOp.QuerySample:
                            QuerySample(ts, rng, liveDocIds);
                            break;
                        case ChurnOp.BatchPurge:
                            BatchPurgeDeleted(ts);
                            break;
                    }

                    if (round % cfg.CheckpointEvery == 0 || round == cfg.Rounds)
                    {
                        ts.SaveChanges();
                        checkpoints.Add(Snapshot(eng, dbPath, $"轮次 {round}", liveDocIds.Count));
                    }
                }
            });

            Phase("4. 空位复用对照实验", () =>
            {
                var reuse = HoleReuseExperiment(eng, dbPath, cfg, rng);
                checkpoints.Add(reuse.AfterDelete);
                checkpoints.Add(reuse.AfterReuse);
                checkpoints.Add(reuse.BaselineFresh);
                Console.WriteLine($"     · 删长写短后 LastPage={reuse.AfterReuse.Metrics.LastPage}  全新库 LastPage={reuse.BaselineFresh.Metrics.LastPage}  复用率≈{reuse.ReuseRatio:P0}");
            });

            total.Stop();
            var final = Snapshot(eng, dbPath, "最终", liveDocIds.Count);
            checkpoints.Add(final);

            var report = BuildReport(cfg, dbPath, checkpoints, total.Elapsed.TotalMilliseconds);
            Console.WriteLine();
            Console.WriteLine(report);
            File.WriteAllText(reportPath, report, Encoding.UTF8);
            Console.WriteLine($"报告已写入 {reportPath}");

            if (!cfg.Keep)
                eng.SetDestoryOnDisposed();
        }

        static void Phase(string name, Action act)
        {
            Console.Write($"  {name} … ");
            var sw = Stopwatch.StartNew();
            act();
            sw.Stop();
            Console.WriteLine($"{sw.Elapsed.TotalMilliseconds:N1} ms");
        }

        static void Bootstrap(DbEngine eng)
        {
            using var ts = eng.StartTransaction();
            Must(ts.Create<ChurnDoc>(ChurnTables.Doc));
            Must(ts.Create<ChurnComment>(ChurnTables.Comment));
        }

        static void SeedLongForm(DbEngine eng, string dbPath, ChurnConfig cfg, Random rng, List<Checkpoint> cps)
        {
            using var ts = eng.StartTransaction();
            for (int d = 1; d <= cfg.Docs; d++)
            {
                Must(ts.Insert(ChurnTables.Doc, new ChurnDoc
                {
                    DocId = d,
                    Title = $"DOC-{d:D5}",
                    Body = Lorem(rng, cfg.LongBodyMin, cfg.LongBodyMax),
                    Revision = 1,
                    State = 0,
                    UpdatedUtc = Utc(d),
                }));

                for (int c = 0; c < cfg.CommentsPerDoc; c++)
                {
                    int cid = (d - 1) * cfg.CommentsPerDoc + c + 1;
                    Must(ts.Insert(ChurnTables.Comment, new ChurnComment
                    {
                        CommentId = cid,
                        DocId = d,
                        Body = Lorem(rng, 200, 800),
                        State = 0,
                    }));
                }
            }
            ts.SaveChanges();
            cps.Add(Snapshot(eng, dbPath, "初稿灌入", cfg.Docs));
        }

        static ChurnOp PickOp(Random rng, int round)
        {
            int wDel = round > 200 ? 28 : 18;
            int wRep = round > 300 ? 22 : 12;
            var options = new (ChurnOp op, int w)[]
            {
                (ChurnOp.UpdateShrink, 22),
                (ChurnOp.UpdateGrow, 18),
                (ChurnOp.UpdateTitle, 8),
                (ChurnOp.DeleteDoc, wDel),
                (ChurnOp.ReplaceDoc, wRep),
                (ChurnOp.CommentChurn, 14),
                (ChurnOp.QuerySample, 5),
                (ChurnOp.BatchPurge, 3),
            };
            int sum = options.Sum(x => x.w);
            int x = rng.Next(sum);
            foreach (var (op, w) in options)
            {
                x -= w;
                if (x < 0) return op;
            }
            return options[^1].op;
        }

        static void UpdateBodies(ITransaction ts, Random rng, List<int> liveDocIds, bool shrink)
        {
            if (liveDocIds.Count == 0) return;
            int docId = liveDocIds[rng.Next(liveDocIds.Count)];
            if (!ChurnDocOps.Exists(ts, docId)) return;

            int len = shrink ? rng.Next(80, 400) : rng.Next(3000, 9000);
            int rev = ChurnDocOps.ReadRevision(ts, docId) + 1;
            ChurnDocOps.UpdateBody(ts, docId, Lorem(rng, len, len + 40), rev, DateTime.UtcNow);
        }

        static void UpdateTitles(ITransaction ts, Random rng, List<int> liveDocIds)
        {
            if (liveDocIds.Count == 0) return;
            int docId = liveDocIds[rng.Next(liveDocIds.Count)];
            if (!ChurnDocOps.Exists(ts, docId)) return;
            int rev = ChurnDocOps.ReadRevision(ts, docId) + 1;
            ChurnDocOps.UpdateTitle(ts, docId, $"DOC-{docId:D5}-R{rev}", rev, DateTime.UtcNow);
        }

        static int HardDeleteDocs(ITransaction ts, Random rng, List<int> liveDocIds, int nextDocId, ChurnConfig cfg, Random seed)
        {
            if (liveDocIds.Count <= cfg.MinLiveDocs) return nextDocId;
            int n = 1 + rng.Next(Math.Min(3, liveDocIds.Count - cfg.MinLiveDocs));
            for (int i = 0; i < n; i++)
            {
                int idx = rng.Next(liveDocIds.Count);
                int docId = liveDocIds[idx];
                liveDocIds.RemoveAt(idx);

                ChurnDocOps.DeleteCommentsForDoc(ts, docId);
                ChurnDocOps.DeleteByDocId(ts, docId);

                // 同轮补一条新文档（测 DataPage slot 复用）
                Must(ts.Insert(ChurnTables.Doc, new ChurnDoc
                {
                    DocId = nextDocId,
                    Title = $"NEW-{nextDocId:D5}",
                    Body = Lorem(seed, 120, 600),
                    Revision = 1,
                    State = 0,
                    UpdatedUtc = DateTime.UtcNow,
                }));
                liveDocIds.Add(nextDocId++);
            }
            return nextDocId;
        }

        static void ReplaceDoc(ITransaction ts, Random rng, List<int> liveDocIds, ref int nextDocId, ChurnConfig cfg, Random seed)
        {
            if (liveDocIds.Count == 0) return;
            int docId = liveDocIds[rng.Next(liveDocIds.Count)];
            if (!ChurnDocOps.Exists(ts, docId)) return;

            ChurnDocOps.DeleteByDocId(ts, docId);
            liveDocIds.Remove(docId);

            int newId = nextDocId++;
            Must(ts.Insert(ChurnTables.Doc, new ChurnDoc
            {
                DocId = newId,
                Title = $"REPL-{newId:D5}",
                Body = Lorem(seed, cfg.LongBodyMin / 2, cfg.LongBodyMax / 2),
                Revision = 1,
                State = 0,
                UpdatedUtc = DateTime.UtcNow,
            }));
            liveDocIds.Add(newId);
        }

        static void CommentChurn(ITransaction ts, Random rng, List<int> liveDocIds, ref int nextCommentId, ChurnConfig cfg)
        {
            if (liveDocIds.Count == 0) return;
            int docId = liveDocIds[rng.Next(liveDocIds.Count)];

            if (rng.Next(2) == 0)
                ChurnDocOps.DeleteOneCommentForDoc(ts, rng, docId);

            Must(ts.Insert(ChurnTables.Comment, new ChurnComment
            {
                CommentId = nextCommentId++,
                DocId = docId,
                Body = Lorem(rng, rng.Next(2) == 0 ? 50 : 1500, rng.Next(2) == 0 ? 200 : 4000),
                State = 0,
            }));
        }

        static void QuerySample(ITransaction ts, Random rng, List<int> liveDocIds)
        {
            if (liveDocIds.Count == 0) return;
            int docId = liveDocIds[rng.Next(liveDocIds.Count)];
            _ = ChurnDocOps.Exists(ts, docId);
            _ = ChurnDocOps.CountComments(ts, docId);
        }

        static void BatchPurgeDeleted(ITransaction ts)
        {
            _ = ChurnDocOps.PurgeDeletedComments(ts);
        }

        static (Checkpoint AfterDelete, Checkpoint AfterReuse, Checkpoint BaselineFresh, double ReuseRatio) HoleReuseExperiment(
            DbEngine eng, string dbPath, ChurnConfig cfg, Random rng)
        {
            const string tab = "hole_lab";
            const int n = 40;

            // A: 灌长文 → 删除 → 灌短文（应复用 Var hole + Data slot）
            using (var ts = eng.StartTransaction())
            {
                Must(ts.Create(tab, [
                    ("id", DbValueType.Int, true),
                    ("body", DbValueType.StrVar, false),
                ]));
                for (int i = 0; i < n; i++)
                    Must(ts.Insert(tab, [("id", i), ("body", Lorem(rng, 5000, 8000))]));
            }

            using (var ts = eng.StartTransaction())
            {
                for (int i = 0; i < n; i++)
                    Must(ts.Delete(tab, "id", i));
                ts.SaveChanges();
            }
            var afterDelete = Snapshot(eng, dbPath, "对照·删长文后", -1);

            using (var ts = eng.StartTransaction())
            {
                for (int i = 0; i < n; i++)
                    Must(ts.Insert(tab, [("id", 1000 + i), ("body", Lorem(rng, 40, 120))]));
                ts.SaveChanges();
            }
            var afterReuse = Snapshot(eng, dbPath, "对照·短文写入后", -1);

            // B: 全新库同样短文
            var freshPath = Path.ChangeExtension(dbPath, ".fresh.db");
            if (File.Exists(freshPath)) File.Delete(freshPath);
            Checkpoint baseline;
            using (var eng2 = new DbEngine(freshPath, true))
            using (var ts = eng2.StartTransaction())
            {
                Must(ts.Create(tab, [
                    ("id", DbValueType.Int, true),
                    ("body", DbValueType.StrVar, false),
                ]));
                for (int i = 0; i < n; i++)
                    Must(ts.Insert(tab, [("id", i), ("body", Lorem(rng, 40, 120))]));
                ts.SaveChanges();
                baseline = Snapshot(eng2, freshPath, "对照·全新短文库", -1);
                eng2.SetDestoryOnDisposed();
            }

            double ratio = baseline.Metrics.LastPage == 0 ? 1 :
                1.0 - (afterReuse.Metrics.LastPage - afterDelete.Metrics.LastPage) /
                (double)Math.Max(1, baseline.Metrics.LastPage);

            return (afterDelete, afterReuse, baseline, Math.Clamp(ratio, 0, 1));
        }

        static Checkpoint Snapshot(DbEngine eng, string dbPath, string label, int liveDocs)
        {
            long fileBytes = ReadFileLength(dbPath);

            using var ts = eng.StartTransactionReadonly();
            ts.GoThrough(ChurnTables.Doc, (ref RowView _) => true);
            ts.GoThrough(ChurnTables.Comment, (ref RowView _) => true);

            var lum = (LumTransaction)ts;
            var m = lum.InspectSpace() with { FileBytesEstimate = fileBytes };

            int docCount = (int)ts.Count(ChurnTables.Doc, (ref RowView r) => true).Value;
            int commentCount = (int)ts.Count(ChurnTables.Comment, (ref RowView r) => true).Value;

            return new Checkpoint(label, liveDocs >= 0 ? liveDocs : docCount, docCount, commentCount, fileBytes, m);
        }

        static long ReadFileLength(string path)
        {
            if (!File.Exists(path)) return -1;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return fs.Length;
        }

        static string BuildReport(ChurnConfig cfg, string dbPath, List<Checkpoint> cps, double wallMs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  长期编辑 / 删改模拟报告");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  命令  ConsoleTest --churn-only --seed={cfg.Seed} --docs={cfg.Docs} --rounds={cfg.Rounds}");
            sb.AppendLine($"  库    {dbPath}");
            sb.AppendLine($"  墙钟  {wallMs:N0} ms");
            sb.AppendLine();
            sb.AppendLine("── 检查点（文件大小 · 页 · 空位）────────────────────────────────");
            sb.AppendLine($"  {"阶段",-18} {"文件",10} {"LastPg",7} {"FreePg",8} {"Data槽",12} {"Var死穴",14} {"文档",6} {"评论",6}");
            foreach (var c in cps)
            {
                var m = c.Metrics;
                int freeSlots = m.DataSlotsCapacity - m.DataSlotsLive;
                sb.AppendLine($"  {c.Label,-18} {FmtBytes(c.FileBytes),10} {m.LastPage,7} {FmtFree(m.FreePageHead),8} {m.DataSlotsLive}/{freeSlots,5} {m.VarNodesDead,4}({FmtBytes(m.VarDeadBytes),8}) {c.DocRows,6} {c.CommentRows,6}");
            }
            sb.AppendLine();
            sb.AppendLine("── 指标说明 ─────────────────────────────────────────────────────");
            sb.AppendLine("  Data槽  存活行/空闲槽（删除后 slot 回收到页内空闲链，可再 Insert）");
            sb.AppendLine("  Var死穴 已删 Var 节点数(及保留字节)；新值能单节点放入时会 best-fit 复用");
            sb.AppendLine("  LastPg   文件曾分配的最高页号；回收页进 FreePg 链，文件体积不缩小");
            sb.AppendLine("  FreePg   全局空闲页链头；≠Max 表示有整页被回收可复用");
            sb.AppendLine();

            if (cps.Count >= 2)
            {
                var first = cps[0];
                var last = cps[^1];
                sb.AppendLine("── 长期编辑结论 ─────────────────────────────────────────────────");
                sb.AppendLine($"  文件  {FmtBytes(first.FileBytes)} → {FmtBytes(last.FileBytes)}  (Δ {FmtBytes(last.FileBytes - first.FileBytes)})");
                sb.AppendLine($"  LastPage  {first.Metrics.LastPage} → {last.Metrics.LastPage}");
                sb.AppendLine($"  Var 死穴  {first.Metrics.VarNodesDead} → {last.Metrics.VarNodesDead}  死字节 {FmtBytes(first.Metrics.VarDeadBytes)} → {FmtBytes(last.Metrics.VarDeadBytes)}");
                bool slotReuse = last.Metrics.DataSlotsCapacity > 0 &&
                    (double)(last.Metrics.DataSlotsCapacity - last.Metrics.DataSlotsLive) / last.Metrics.DataSlotsCapacity > 0.05;
                bool varHoles = last.Metrics.VarNodesDead > 0;
                bool freePages = last.Metrics.FreePageHead != uint.MaxValue;
                sb.AppendLine($"  固定行 slot 复用  {(slotReuse ? "有（存在空闲槽）" : "不明显")}");
                sb.AppendLine($"  Var hole 保留     {(varHoles ? "有（删除留穴，待 best-fit）" : "无")}");
                sb.AppendLine($"  整页回收链        {(freePages ? "有" : "无")}");
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            return sb.ToString();
        }

        static string FmtBytes(long n) =>
            n < 0 ? "n/a" : n < 1024 ? $"{n} B" : n < 1024 * 1024 ? $"{n / 1024.0:N1} KB" : $"{n / (1024.0 * 1024.0):N2} MB";

        static string FmtFree(uint free) => free == uint.MaxValue ? "-" : free.ToString();

        static int Pick(Random rng, int[] weightedPairs)
        {
            int sum = 0;
            for (int i = 1; i < weightedPairs.Length; i += 2) sum += weightedPairs[i];
            int x = rng.Next(sum);
            for (int i = 0; i < weightedPairs.Length; i += 2)
            {
                x -= weightedPairs[i + 1];
                if (x < 0) return weightedPairs[i];
            }
            return weightedPairs[^2];
        }

        static string Lorem(Random rng, int min, int max)
        {
            int n = min + rng.Next(Math.Max(1, max - min + 1));
            Span<char> buf = n <= 512 ? stackalloc char[n] : new char[n];
            const string abc = "abcdefghijklmnopqrstuvwxyz0123456789 \n";
            for (int i = 0; i < n; i++) buf[i] = abc[rng.Next(abc.Length)];
            return new string(buf);
        }

        static DateTime Utc(int seq) => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(seq);

        static void Must(LumDbEngine.Element.Engine.Results.IDbResult r)
        {
            if (!r.IsSuccess)
                throw new InvalidOperationException(r.Exception?.Message ?? "db failed");
        }

        enum ChurnOp : int
        {
            UpdateShrink, UpdateGrow, UpdateTitle,
            DeleteDoc, ReplaceDoc, CommentChurn,
            QuerySample, BatchPurge,
        }

        sealed record Checkpoint(string Label, int LiveDocs, int DocRows, int CommentRows, long FileBytes, DbDiagnostics.SpaceMetrics Metrics);

        sealed class ChurnConfig
        {
            public int Seed = 20260827;
            public int Docs = 120;
            public int CommentsPerDoc = 5;
            public int Rounds = 400;
            public int CheckpointEvery = 50;
            public int MinLiveDocs = 30;
            public int LongBodyMin = 3500;
            public int LongBodyMax = 7500;
            public bool Keep;
            public int TimeoutMs = 120_000;

            public static ChurnConfig Parse(string[] args)
            {
                var c = new ChurnConfig();
                foreach (var a in args)
                {
                    if (TryNum(a, "--seed=", out int seed)) c.Seed = seed;
                    else if (TryNum(a, "--docs=", out int docs)) c.Docs = Math.Clamp(docs, 10, 5000);
                    else if (TryNum(a, "--rounds=", out int r)) c.Rounds = Math.Clamp(r, 20, 50_000);
                    else if (TryNum(a, "--comments=", out int cm)) c.CommentsPerDoc = Math.Clamp(cm, 1, 50);
                    else if (a.Equals("--keep", StringComparison.OrdinalIgnoreCase)) c.Keep = true;
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

    // ChurnComment 列序（固定列 RowView 下标；Body 为 StrVar）
    internal static class ChurnCols
    {
        public const int CommentDocId = 1;
        public const int CommentState = 3;
        public const int DocRevision = 3;
    }

    internal static class ChurnDocOps
    {
        /// <summary>按键查找/更新/删除，不调用 FindEntity，避免 VS 在未跑源生成器时误报 CS0311。</summary>
        public static bool Exists(ITransaction ts, int docId) =>
            ts.Find(ChurnTables.Doc, "DocId", docId).IsSuccess;

        public static void UpdateBody(ITransaction ts, int docId, string body, int revision, DateTime updatedUtc)
        {
            Must(ts.Update(ChurnTables.Doc, "DocId", docId, "Body", body));
            Must(ts.Update(ChurnTables.Doc, "DocId", docId, "Revision", revision));
            Must(ts.Update(ChurnTables.Doc, "DocId", docId, "UpdatedUtc", updatedUtc));
        }

        public static void UpdateTitle(ITransaction ts, int docId, string title, int revision, DateTime updatedUtc)
        {
            Must(ts.Update(ChurnTables.Doc, "DocId", docId, "Title", title));
            Must(ts.Update(ChurnTables.Doc, "DocId", docId, "Revision", revision));
            Must(ts.Update(ChurnTables.Doc, "DocId", docId, "UpdatedUtc", updatedUtc));
        }

        public static void DeleteByDocId(ITransaction ts, int docId) =>
            Must(ts.Delete(ChurnTables.Doc, "DocId", docId));

        public static int ReadRevision(ITransaction ts, int docId)
        {
            var res = ts.Find(ChurnTables.Doc, "DocId", docId);
            return res.IsSuccess ? res.Row.GetInt(ChurnCols.DocRevision) : 0;
        }

        public static void DeleteCommentsForDoc(ITransaction ts, int docId)
        {
            var ids = new List<uint>();
            ts.GoThrough(ChurnTables.Comment, (uint id, ref RowView row) =>
            {
                if (row.GetInt(ChurnCols.CommentDocId) == docId
                    && row.GetInt(ChurnCols.CommentState) == 0)
                    ids.Add(id);
                return true;
            });
            foreach (var id in ids)
                Must(ts.Delete(ChurnTables.Comment, id));
        }

        public static void DeleteOneCommentForDoc(ITransaction ts, Random rng, int docId)
        {
            var ids = new List<uint>();
            ts.GoThrough(ChurnTables.Comment, (uint id, ref RowView row) =>
            {
                if (row.GetInt(ChurnCols.CommentDocId) == docId
                    && row.GetInt(ChurnCols.CommentState) == 0)
                    ids.Add(id);
                return true;
            });
            if (ids.Count == 0) return;
            Must(ts.Delete(ChurnTables.Comment, ids[rng.Next(ids.Count)]));
        }

        public static int CountComments(ITransaction ts, int docId) =>
            (int)ts.Count(ChurnTables.Comment, (ref RowView row) =>
                row.GetInt(ChurnCols.CommentDocId) == docId
                && row.GetInt(ChurnCols.CommentState) == 0).Value;

        public static int PurgeDeletedComments(ITransaction ts)
        {
            var ids = new List<uint>();
            ts.GoThrough(ChurnTables.Comment, (uint id, ref RowView row) =>
            {
                if (row.GetInt(ChurnCols.CommentState) == 9)
                    ids.Add(id);
                return true;
            });
            foreach (var id in ids)
                Must(ts.Delete(ChurnTables.Comment, id));
            return ids.Count;
        }

        static void Must(LumDbEngine.Element.Engine.Results.IDbResult r)
        {
            if (!r.IsSuccess)
                throw new InvalidOperationException(r.Exception?.Message ?? "db failed");
        }
    }
}
