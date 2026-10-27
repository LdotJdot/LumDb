using System.Diagnostics;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;

namespace UnitTestLumDb.DefectHunt
{
    /// <summary>
    /// 性能瓶颈现场测量（绿=测量完成，数值写入 TestEvidence）。
    /// P1: 每个事务独立 DbCache —— 跨事务无共享缓冲池，点查每次重新走页链 IO。
    /// P2: 每次提交的固定成本（.log 创建/写/回放/删除 + fsync）。
    /// P3: 只读事务持有 Read 锁期间，写事务的 SaveChanges 被整体阻塞（无 MVCC/快照）。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DefectHunt_PerfProbes
    {
        private const string Table = "perf";
        private const int Rows = 20_000;

        private static string Pad(int i) => new string((char)('a' + i % 26), 60) + i.ToString("D6");

        private static void Seed(DbEngine eng, int rows)
        {
            using var ts = eng.StartTransaction();
            ts.Create(Table, [("k", DbValueType.Int, true), ("pad", DbValueType.StrVar, false)]);
            for (int i = 0; i < rows; i++)
                ts.Insert(Table, [("k", i), ("pad", Pad(i))]);
            ts.SaveChanges();
        }

        [TestMethod]
        public void P1_PerTransactionColdCache_PointReadCost()
        {
            var path = DhSupport.TempPath("p1");
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                var seedSw = Stopwatch.StartNew();
                Seed(eng, Rows);
                seedSw.Stop();

                const int probes = 400;
                double warmUs;
                double coldUs;

                using (var ro = eng.StartTransactionReadonly())
                {
                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < probes; i++)
                    {
                        var r = ro.Find(Table, "k", (i * 47) % Rows);
                        if (!r.IsSuccess) Assert.Fail("warm find failed");
                    }
                    sw.Stop();
                    warmUs = sw.Elapsed.TotalMilliseconds * 1000.0 / probes;
                }

                {
                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < probes; i++)
                    {
                        using var ro = eng.StartTransactionReadonly();
                        var r = ro.Find(Table, "k", (i * 47) % Rows);
                        if (!r.IsSuccess) Assert.Fail("cold find failed");
                    }
                    sw.Stop();
                    coldUs = sw.Elapsed.TotalMilliseconds * 1000.0 / probes;
                }

                double ratio = coldUs / Math.Max(warmUs, 0.001);
                var msg = $"warm={warmUs:F1}us/op, cold={coldUs:F1}us/op, ratio={ratio:F2}x, " +
                          $"seed({Rows})={seedSw.ElapsedMilliseconds}ms";

                DhSupport.Evidence("P1 每事务冷缓存：点查成本",
                    ("行数", Rows),
                    ("同事务预热后", $"{warmUs:F1} us/op"),
                    ("每查一个新事务", $"{coldUs:F1} us/op"),
                    ("冷/热倍率", $"{ratio:F2}x"),
                    ("建表+20k 行写入", $"{seedSw.ElapsedMilliseconds} ms"));

                Console.WriteLine("[P1] " + msg);
                Assert.IsTrue(coldUs > warmUs, "冷缓存点查应明显慢于同事务内重复点查");
            }
            finally
            {
                try { eng?.Dispose(); } catch { }
                DhSupport.TryDelete(path, path + ".log");
            }
        }

        [TestMethod]
        public void P2_CommitThroughput_SmallAndEmptyTransactions()
        {
            const int n = 200;
            var path = DhSupport.TempPath("p2");
            try
            {
                double fileOps;
                double emptyOps;
                double memOps;

                using (var eng = new DbEngine(path))
                {
                    using (var ts = eng.StartTransaction())
                        ts.Create(Table, [("k", DbValueType.Int, true), ("pad", DbValueType.StrVar, false)]);

                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < n; i++)
                    {
                        using var ts = eng.StartTransaction();
                        ts.Insert(Table, [("k", i), ("pad", "x")]);
                    }
                    sw.Stop();
                    fileOps = n / Math.Max(sw.Elapsed.TotalSeconds, 0.001);

                    var sw2 = Stopwatch.StartNew();
                    for (int i = 0; i < n; i++)
                    {
                        using var ts = eng.StartTransaction();
                    }
                    sw2.Stop();
                    emptyOps = n / Math.Max(sw2.Elapsed.TotalSeconds, 0.001);
                }

                using (var mem = new DbEngine())
                {
                    using (var ts = mem.StartTransaction())
                        ts.Create(Table, [("k", DbValueType.Int, true), ("pad", DbValueType.StrVar, false)]);

                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < n; i++)
                    {
                        using var ts = mem.StartTransaction();
                        ts.Insert(Table, [("k", i), ("pad", "x")]);
                    }
                    sw.Stop();
                    memOps = n / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
                }

                var msg = $"file 1-insert tx={fileOps:F0}/s, file empty tx={emptyOps:F0}/s, memory 1-insert tx={memOps:F0}/s";
                DhSupport.Evidence("P2 小事务提交吞吐",
                    ("文件引擎 单行事务", $"{fileOps:F0} tx/s"),
                    ("文件引擎 空提交（无脏页）", $"{emptyOps:F0} tx/s"),
                    ("内存引擎 单行事务", $"{memOps:F0} tx/s"),
                    ("样本数", n));
                Console.WriteLine("[P2] " + msg);
                Assert.IsTrue(fileOps > 0 && memOps > 0);
            }
            finally
            {
                DhSupport.TryDelete(path, path + ".log");
            }
        }

        [TestMethod]
        public void P3_ReaderTransaction_BlocksWriterCommit()
        {
            var path = DhSupport.TempPath("p3");
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                Seed(eng, Rows);

                // 基线：无并发读者时的提交耗时
                double baselineMs = 0;
                for (int i = 0; i < 3; i++)
                {
                    var sw = Stopwatch.StartNew();
                    using (var ts = eng.StartTransaction())
                    {
                        ts.Insert(Table, [("k", 900_000 + i), ("pad", "base")]);
                        ts.SaveChanges();
                    }
                    sw.Stop();
                    baselineMs = Math.Max(baselineMs, sw.Elapsed.TotalMilliseconds);
                }

                long scanMs = 0;
                int observed = 0;
                using var started = new ManualResetEventSlim(false);
                var reader = new Thread(() =>
                {
                    using var ro = eng.StartTransactionReadonly();
                    started.Set();
                    var sw = Stopwatch.StartNew();
                    int count = 0;
                    // 200 次全表扫描，拉长持锁窗口，测量写者在读者活跃时的提交延迟。
                    for (int s = 0; s < 200; s++)
                        ro.GoThrough(Table, (ref RowView _) => { count++; return true; });
                    sw.Stop();
                    scanMs = sw.ElapsedMilliseconds;
                    observed = count / 200;
                });
                reader.Start();
                started.Wait();
                Thread.Sleep(20); // 确保读者已进入扫描循环

                var swStall = Stopwatch.StartNew();
                string err = "-";
                try
                {
                    using var ts = eng.StartTransaction();
                    ts.Insert(Table, [("k", 910_000), ("pad", "stalled")]);
                    ts.SaveChanges();
                }
                catch (Exception ex)
                {
                    err = DhSupport.Short(ex);
                }
                swStall.Stop();
                reader.Join();

                double stallMs = swStall.Elapsed.TotalMilliseconds;
                var msg = $"baseline={baselineMs:F1}ms, with-reader={stallMs:F1}ms, reader-scan={scanMs}ms, rows={observed}, err={err}";

                DhSupport.Evidence("P3 只读事务阻塞写提交",
                    ("无读者提交耗时", $"{baselineMs:F1} ms"),
                    ("读者扫描期间提交耗时", $"{stallMs:F1} ms"),
                    ("读者扫描耗时", $"{scanMs} ms"),
                    ("读者扫描行数", observed),
                    ("提交异常", err));

                Console.WriteLine("[P3] " + msg);
                Assert.IsTrue(stallMs >= baselineMs,
                    $"读者扫描期间写提交({stallMs:F1}ms)应不短于基线({baselineMs:F1}ms)");
            }
            finally
            {
                try { eng?.Dispose(); } catch { }
                DhSupport.TryDelete(path, path + ".log");
            }
        }
    }
}