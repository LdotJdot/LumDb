using System.Diagnostics;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.DefectHunt
{
    /// <summary>
    /// 修复验证用例（绿 = 修复生效，红 = 缺陷仍在）。
    /// DH1 提交 IO 失败：磁盘错误必须抛出，但引擎写租约必须归还、引擎不得砖化、故障排除后可重试。
    /// DH2 跨线程 Dispose：不得抛 SynchronizationLockException，租约归还，引擎仍可用。
    /// DH3 事务池：第 33 个事务快速失败并给出准确错误（TooManyOpenTransactions），而不是 3s 后谎称引擎已释放。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DefectHunt_LockAndLifecycleTests
    {
        private const string Table = "dh";

        private static void Seed(DbEngine eng)
        {
            using var ts = eng.StartTransaction();
            ts.Create(Table, [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
            ts.Insert(Table, [("k", 1), ("v", "seed")]);
        }

        [TestMethod]
        public void DH1_CommitIoFailure_Throws_ReleasesLease_AndRetryCommits()
        {
            var path = DhSupport.TempPath("dh1");
            DbEngine? eng = null;
            var hostage = path + ".log";
            try
            {
                eng = new DbEngine(path);
                Seed(eng); // 基线：一次正常提交

                // 用“目录占位”让 .log 无法创建：SaveCurrentPageCache -> DbLog.Create 抛 UnauthorizedAccessException。
                Directory.CreateDirectory(hostage);

                var tx = eng.StartTransaction();
                tx.Insert(Table, [("k", 2), ("v", "will-fail")]);

                var commitSw = Stopwatch.StartNew();
                var commitEx = DhSupport.Capture(tx.SaveChanges);
                commitSw.Stop();

                // Dispose 会再次尝试落盘（该事务未提交）。异常可以继续抛，但租约必须归还。
                var disposeEx = DhSupport.Capture(tx.Dispose);

                // 同线程探针：写租约若泄漏，这里会在 TimeoutMilliseconds 之后失败。
                var probeSw = Stopwatch.StartNew();
                var probeEx = DhSupport.Capture(() =>
                {
                    using var probe = eng.StartTransaction();
                    if (!probe.Find(Table, "k", 1).IsSuccess)
                        throw new InvalidOperationException("probe could not read the seed row");
                });
                probeSw.Stop();

                // 跨线程探针：确认写租约不是靠“同线程重入”蒙混过关。
                Exception? crossEx = null;
                long crossMs = 0;
                var worker = new Thread(() =>
                {
                    var sw = Stopwatch.StartNew();
                    crossEx = DhSupport.Capture(() =>
                    {
                        using var cross = eng.StartTransaction();
                        if (!cross.Find(Table, "k", 1).IsSuccess)
                            throw new InvalidOperationException("cross-thread probe could not read the seed row");
                    });
                    sw.Stop();
                    crossMs = sw.ElapsedMilliseconds;
                });
                worker.Start();
                worker.Join();

                // 排除故障后重试：一次磁盘错误 = 一次失败，不是永久报废。
                Directory.Delete(hostage);
                Exception? retryEx = null;
                var retrySw = Stopwatch.StartNew();
                retryEx = DhSupport.Capture(() =>
                {
                    using var retry = eng.StartTransaction();
                    retry.Insert(Table, [("k", 3), ("v", "after-retry")]);
                    retry.SaveChanges();
                });
                retrySw.Stop();

                bool k1, k2, k3;
                int rows;
                using (var check = eng.StartTransaction())
                {
                    k1 = check.Find(Table, "k", 1).IsSuccess;
                    k2 = check.Find(Table, "k", 2).IsSuccess;
                    k3 = check.Find(Table, "k", 3).IsSuccess;
                    rows = RowScan.Count(check, Table);
                }

                DhSupport.Evidence("DH1 提交 IO 失败 -> 抛异常但不砖化、可重试",
                    ("db 路径", path),
                    ("SaveChanges 异常", DhSupport.Describe(commitEx)),
                    ("SaveChanges 耗时(ms)", commitSw.ElapsedMilliseconds),
                    ("Dispose 异常", DhSupport.Describe(disposeEx)),
                    ("同线程探针异常", DhSupport.Describe(probeEx)),
                    ("同线程探针耗时(ms)", probeSw.ElapsedMilliseconds),
                    ("跨线程探针异常", DhSupport.Describe(crossEx)),
                    ("跨线程探针耗时(ms)", crossMs),
                    ("排除故障后重试异常", DhSupport.Describe(retryEx)),
                    ("重试耗时(ms)", retrySw.ElapsedMilliseconds),
                    ("提交后 k=1/2/3 是否存在", $"{k1}/{k2}/{k3}"),
                    ("提交后行数", rows));

                if (commitEx == null)
                    Assert.Inconclusive("占位目录未能让提交失败（非 Windows 语义），本用例不适用。");

                Assert.IsTrue(commitEx is IOException || commitEx is UnauthorizedAccessException,
                    $"磁盘错误必须以 IO 异常抛出，实际：{DhSupport.Describe(commitEx)}");
                Assert.IsNull(probeEx, $"一次提交失败后同线程新事务失败：{DhSupport.Describe(probeEx)}");
                Assert.IsTrue(probeSw.ElapsedMilliseconds < eng.TimeoutMilliseconds,
                    $"新事务耗时 {probeSw.ElapsedMilliseconds}ms，说明仍在等泄漏的写租约");
                Assert.IsNull(crossEx, $"一次提交失败后跨线程新事务失败：{DhSupport.Describe(crossEx)}");
                Assert.IsNull(retryEx, $"排除故障后重试仍失败：{DhSupport.Describe(retryEx)}");
                Assert.IsTrue(k1, "已提交的种子行必须仍然存在");
                Assert.IsFalse(k2, "失败提交的行不得落库");
                Assert.IsTrue(k3, "重试提交的行必须落库");
                Assert.AreEqual(2, rows);
            }
            finally
            {
                DhSupport.TryDelete(hostage);
                try { eng?.Dispose(); } catch { }
                DhSupport.TryDelete(path, path + ".log");
            }
        }

        [TestMethod]
        public void DH1b_CommitIoFailure_DirtyPagesKept_SameTransactionRetryCommits()
        {
            var path = DhSupport.TempPath("dh1b");
            DbEngine? eng = null;
            var hostage = path + ".log";
            try
            {
                eng = new DbEngine(path);
                Seed(eng); // 基线：一次正常提交

                Directory.CreateDirectory(hostage); // 让 DbLog.Create 失败

                var tx = eng.StartTransaction();
                Exception? first = null, retry = null, disposeEx = null;
                try
                {
                    Assert.IsTrue(tx.Insert(Table, [("k", 2), ("v", "retry-after-failure")]).IsSuccess);

                    first = DhSupport.Capture(tx.SaveChanges);

                    // 磁盘恢复后，同一个事务直接重试：脏页必须还在（提交失败≠语句失败）。
                    Directory.Delete(hostage);
                    retry = DhSupport.Capture(tx.SaveChanges);
                }
                finally
                {
                    disposeEx = DhSupport.Capture(tx.Dispose);
                }

                bool k1, k2;
                int rows;
                using (var check = eng.StartTransaction())
                {
                    k1 = check.Find(Table, "k", 1).IsSuccess;
                    k2 = check.Find(Table, "k", 2).IsSuccess;
                    rows = RowScan.Count(check, Table);
                }

                DhSupport.Evidence("DH1b 提交 IO 失败 -> 脏页保留 -> 同一事务重试成功",
                    ("db 路径", path),
                    ("首次 SaveChanges 异常", DhSupport.Describe(first)),
                    ("磁盘恢复后同一事务重试异常", DhSupport.Describe(retry)),
                    ("Dispose 异常", DhSupport.Describe(disposeEx)),
                    ("k=1 / k=2 是否存在", $"{k1}/{k2}"),
                    ("行数", rows));

                if (first == null)
                    Assert.Inconclusive("占位目录未能让提交失败（非 Windows 语义），本用例不适用。");

                Assert.IsTrue(first is IOException || first is UnauthorizedAccessException,
                    $"磁盘错误必须以 IO 异常抛出，实际：{DhSupport.Describe(first)}");
                Assert.IsNull(retry, $"磁盘恢复后同一事务重试必须成功，实际：{DhSupport.Describe(retry)}");
                Assert.IsNull(disposeEx, $"提交成功后 Dispose 不得抛异常：{DhSupport.Describe(disposeEx)}");
                Assert.IsTrue(k1, "种子行必须仍然存在");
                Assert.IsTrue(k2, "重试提交的行必须落库");
                Assert.AreEqual(2, rows);
            }
            finally
            {
                DhSupport.TryDelete(hostage);
                try { eng?.Dispose(); } catch { }
                DhSupport.TryDelete(path, path + ".log");
            }
        }

        [TestMethod]
        public void DH2_DisposeOnForeignThread_ReturnsLease_AndKeepsEngineUsable()
        {
            using var eng = new DbEngine();
            Seed(eng);

            // 在 A 线程创建（持有引擎 upgradeable 租约），在 B 线程释放。
            ITransaction? tx = null;
            var creator = new Thread(() => tx = eng.StartTransaction());
            creator.Start();
            creator.Join();
            Assert.IsNotNull(tx, "事务应在工作线程创建成功");

            var disposeSw = Stopwatch.StartNew();
            var disposeEx = DhSupport.Capture(tx!.Dispose);
            disposeSw.Stop();

            var probeSw = Stopwatch.StartNew();
            var probeEx = DhSupport.Capture(() =>
            {
                using var probe = eng.StartTransaction();
                if (!probe.Find(Table, "k", 1).IsSuccess)
                    throw new InvalidOperationException("probe could not read the seed row");
            });
            probeSw.Stop();

            DhSupport.Evidence("DH2 跨线程 Dispose -> 租约归还、引擎可用",
                ("Dispose 异常", DhSupport.Describe(disposeEx)),
                ("Dispose 耗时(ms)", disposeSw.ElapsedMilliseconds),
                ("探针异常", DhSupport.Describe(probeEx)),
                ("探针耗时(ms)", probeSw.ElapsedMilliseconds));

            Assert.IsNull(disposeEx, $"跨线程 Dispose 不应抛异常，实际：{DhSupport.Describe(disposeEx)}");
            Assert.IsNull(probeEx, $"跨线程 Dispose 后引擎不可用：{DhSupport.Describe(probeEx)}");
            Assert.IsTrue(probeSw.ElapsedMilliseconds < eng.TimeoutMilliseconds,
                $"新事务耗时 {probeSw.ElapsedMilliseconds}ms，说明租约未归还");
        }

        [TestMethod]
        public void DH2b_DisposeWithPendingWorkOnForeignThread_CommitsImplicitly_WithoutException()
        {
            using var eng = new DbEngine();
            Seed(eng);

            ITransaction? tx = null;
            var creator = new Thread(() => tx = eng.StartTransaction());
            creator.Start();
            creator.Join();
            Assert.IsNotNull(tx);

            Assert.IsTrue(tx!.Insert(Table, [("k", 2), ("v", "uncommitted")]).IsSuccess);
            var disposeEx = DhSupport.Capture(() => { var worker = new Thread(() => tx!.Dispose()); worker.Start(); worker.Join(); });

            bool k2;
            using (var check = eng.StartTransaction())
                k2 = check.Find(Table, "k", 2).IsSuccess;

            DhSupport.Evidence("DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常",
                ("Dispose 异常", DhSupport.Describe(disposeEx)),
                ("未提交行是否按 Dispose 语义落库", k2));

            Assert.IsNull(disposeEx, $"跨线程 Dispose 不应抛异常：{DhSupport.Describe(disposeEx)}");
            // README: “SaveChanges / Dispose commit”。跨线程 Dispose 与同线程一致：提交并归还租约。
            Assert.IsTrue(k2, "Dispose 是提交点，跨线程 Dispose 也必须把待提交内容落库");
        }

        [TestMethod]
        public void DH3_TransactionPool_FailsFastWithAccurateError_AndRecovers()
        {
            using var eng = new DbEngine();
            Seed(eng);

            var holders = new List<ITransactionReadonly>();
            try
            {
                for (int i = 0; i < DbEngine.MaxConcurrentTransactions; i++)
                    holders.Add(eng.StartTransactionReadonly());

                var fullSw = Stopwatch.StartNew();
                var fullEx = DhSupport.Capture(() => { using var extra = eng.StartTransactionReadonly(); });
                fullSw.Stop();

                bool engineHealthy = holders.All(h => h.Find(Table, "k", 1).IsSuccess);

                // 配置等待窗口后，池满时应等待并再次快速失败（而不是默认 3s 超时）。
                eng.TransactionPoolWaitMilliseconds = 250;
                var waitSw = Stopwatch.StartNew();
                var waitEx = DhSupport.Capture(() => { using var extra = eng.StartTransactionReadonly(); });
                waitSw.Stop();

                // 归还一个槽位后立即可用。
                holders[0].Dispose();
                holders.RemoveAt(0);
                // 本线程仍持有 31 个事务；引擎按设计拒绝“同线程开第二个写事务”，
                // 所以从另一条线程验证槽位归还后的可用性（租约本身与线程无关）。
                Exception? recoverEx = null;
                var recoverSw = Stopwatch.StartNew();
                var recoverThread = new Thread(() => recoverEx = DhSupport.Capture(() => { using var ok = eng.StartTransaction(); }));
                recoverThread.Start();
                recoverThread.Join();
                recoverSw.Stop();

                DhSupport.Evidence("DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复",
                    ("池上限", DbEngine.MaxConcurrentTransactions),
                    ("第 33 个异常", DhSupport.Describe(fullEx)),
                    ("第 33 个耗时(ms)", fullSw.ElapsedMilliseconds),
                    ("错误是否谎称 disposed", fullEx?.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase) ?? false),
                    ("引擎实际健康", engineHealthy),
                    ("配置 250ms 等待后的异常", DhSupport.Describe(waitEx)),
                    ("等待耗时(ms)", waitSw.ElapsedMilliseconds),
                    ("归还槽位后异常", DhSupport.Describe(recoverEx)),
                    ("归还槽位后耗时(ms)", recoverSw.ElapsedMilliseconds));

                Assert.IsNotNull(fullEx, "第 33 个事务必须失败");
                StringAssert.Contains(fullEx!.Message, LumExceptionMessage.TooManyOpenTransactions);
                Assert.IsFalse(fullEx.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase),
                    "池满不得报成“引擎已释放”");
                Assert.IsTrue(fullSw.ElapsedMilliseconds < eng.TimeoutMilliseconds,
                    $"池满必须在超时前失败，实际 {fullSw.ElapsedMilliseconds}ms");
                Assert.IsTrue(engineHealthy, "池满时已持有的事务必须仍然可用");
                Assert.IsNotNull(waitEx);
                Assert.IsTrue(waitSw.ElapsedMilliseconds >= 200,
                    $"配置了 250ms 等待窗口，实际只等了 {waitSw.ElapsedMilliseconds}ms");
                Assert.IsNull(recoverEx, $"归还槽位后仍无法开启事务：{DhSupport.Describe(recoverEx)}");
            }
            finally
            {
                foreach (var h in holders)
                {
                    try { h.Dispose(); } catch { }
                }
            }
        }
    }
}