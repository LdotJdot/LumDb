using System.Diagnostics;
using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;
using UnitTestLumDb.DefectHunt;

namespace UnitTestLumDb.UsageScenarios
{
    /// <summary>
    /// 使用场景覆盖：以调用方视角固化端到端契约，而不是逐个方法做白盒测试。
    /// 场景：CRUD 提交/重开、未提交丢弃、Discard 复用、失败语句作废、async 跨线程、
    /// 并发读写、只读隔离、引擎生命周期、Vacuum/Alter 维护、连接池回收、两套 API 一致性。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public partial class TransactionUsageScenarioTests
    {
        private const string Table = "scn";

        [LumEntity]
        public partial class ScnEntity
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            [Str32B] public string Name { get; set; } = "";
            [StrVar] public string Note { get; set; } = "";
        }

        private static void CreateTable(ITransaction ts)
            => ts.Create(Table, [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);

        private static void SeedCommitted(DbEngine eng, int rows = 3)
        {
            using var ts = eng.StartTransaction();
            CreateTable(ts);
            for (int i = 0; i < rows; i++)
                Assert.IsTrue(ts.Insert(Table, [("k", i), ("v", "v" + i)]).IsSuccess);
            ts.SaveChanges();
        }

        // ---------------------------------------------------------------- 基本生命周期

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_Crud_Commit_AndReopen(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng);

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Update(Table, "k", 1, "v", "v1-updated").IsSuccess);
                    Assert.IsTrue(ts.Delete(Table, "k", 2).IsSuccess);
                    ts.Insert(Table, [("k", 9), ("v", "v9")]);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual("v1-updated", check.Find(Table, "k", 1).Row.GetString(1));
                    Assert.IsFalse(check.Find(Table, "k", 2).IsSuccess, "删除的行不得复现");
                    Assert.AreEqual("v9", check.Find(Table, "k", 9).Row.GetString(1));
                    Assert.AreEqual(3, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_DisposeWithoutSave_CommitsImplicitly(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                using (var ts = eng.StartTransaction())
                {
                    ts.Insert(Table, [("k", 100), ("v", "never")]);
                    ts.Update(Table, "k", 0, "v", "touched");
                    // 不调用 SaveChanges：README 约定 “SaveChanges / Dispose commit”。
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsTrue(check.Find(Table, "k", 0).IsSuccess);
                    Assert.AreEqual("touched", check.Find(Table, "k", 0).Row.GetString(1),
                        "Dispose 是提交点，未 SaveChanges 的更新也必须落库");
                    Assert.IsTrue(check.Find(Table, "k", 100).IsSuccess,
                        "Dispose 是提交点，未 SaveChanges 的插入也必须落库");
                    Assert.AreEqual(2, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_DiscardWithoutSave_LeavesCommittedImageUntouched(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                using (var ts = eng.StartTransaction())
                {
                    ts.Insert(Table, [("k", 100), ("v", "never")]);
                    ts.Update(Table, "k", 0, "v", "touched");
                    ts.Discard();
                    // Discard 之后再 Dispose 不得把回滚掉的内容写回去。
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsFalse(check.Find(Table, "k", 100).IsSuccess, "Discard 之后的 Dispose 不得落库");
                    Assert.AreEqual("v0", check.Find(Table, "k", 0).Row.GetString(1));
                    Assert.AreEqual(1, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_Discard_RollsBack_AndTransactionStaysUsable(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                // 建表必须先提交：Discard 回滚整个工作集，包含同事务里未提交的 CREATE TABLE。
                using (var setup = eng.StartTransaction())
                {
                    CreateTable(setup);
                    setup.SaveChanges();
                }

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 1), ("v", "rolled-back")]).IsSuccess);
                    ts.Discard();

                    Assert.AreEqual(0, RowScan.Count(ts, Table), "Discard 之后工作集必须回到已提交镜像");
                    Assert.IsTrue(ts.Insert(Table, [("k", 2), ("v", "kept")]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsFalse(check.Find(Table, "k", 1).IsSuccess);
                    Assert.AreEqual("kept", check.Find(Table, "k", 2).Row.GetString(1));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_CommittedUpdate_IsVisibleToNewReaders(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 5);

                // 先读一遍旧值，让读句柄池里的句柄命中这些页；之后的提交必须让它们失效。
                using (var ro = eng.StartTransactionReadonly())
                {
                    Assert.AreEqual("v0", ro.Find(Table, "k", 0).Row.GetString(1));
                    Assert.AreEqual("v3", ro.Find(Table, "k", 3).Row.GetString(1));
                }

                for (int round = 0; round < 5; round++)
                {
                    using (var w = eng.StartTransaction())
                    {
                        Assert.IsTrue(w.Update(Table, "k", 3, "v", $"v3-r{round}").IsSuccess);
                        w.SaveChanges();
                    }

                    using var reader = eng.StartTransactionReadonly();
                    Assert.AreEqual($"v3-r{round}", reader.Find(Table, "k", 3).Row.GetString(1),
                        $"第 {round} 轮提交后新只读事务读到了旧页（读句柄缓存未失效）");
                    Assert.AreEqual("v0", reader.Find(Table, "k", 0).Row.GetString(1), "未修改的行必须保持原值");
                }
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_FailedWrite_AbortsTransaction_AndNothingIsPersisted(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng);

                var tx = eng.StartTransaction();
                try
                {
                    Assert.IsTrue(tx.Insert(Table, [("k", 50), ("v", "before-failure")]).IsSuccess);
                    Assert.IsNotNull(DhSupport.Capture(() => tx.Insert(Table, [("k", 50), ("v", "dup")])));

                    var rejected = DhSupport.Capture(() => tx.Find(Table, "k", 0));
                    var commit = DhSupport.Capture(tx.SaveChanges);
                    Assert.IsTrue(DhSupport.IsAborted(rejected), $"作废事务的读语句也必须被拒绝：{DhSupport.Describe(rejected)}");
                    Assert.IsTrue(DhSupport.IsAborted(commit), $"作废事务不得提交：{DhSupport.Describe(commit)}");
                }
                finally
                {
                    try { tx.Dispose(); } catch { }
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsFalse(check.Find(Table, "k", 50).IsSuccess);
                    Assert.AreEqual(3, RowScan.Count(check, Table), "已提交数据不受失败事务影响");
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        // ---------------------------------------------------------------- 跨线程 / async

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public async Task Scenario_AsyncTransaction_CommittedFromContinuationThread(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                // 事务在池线程创建，await 之后可能在线程池的另一条线程上提交与释放。
                var tx = await Task.Run(() => eng.StartTransaction());
                await Task.Delay(20);
                tx.Insert(Table, [("k", 77), ("v", "async")]);
                await Task.Yield();
                tx.SaveChanges();
                await Task.Run(tx.Dispose);

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual("async", check.Find(Table, "k", 77).Row.GetString(1));
                    Assert.AreEqual(2, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_ConcurrentReaders_WhileWriterCommits(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 50);
                var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
                using var stop = new CancellationTokenSource();

                var readers = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
                {
                    try
                    {
                        while (!stop.IsCancellationRequested)
                        {
                            using var ro = eng.StartTransactionReadonly();
                            int n = 0;
                            ro.GoThrough(Table, (ref RowView _) => { n++; return true; });
                            if (n < 50) throw new InvalidOperationException($"reader saw {n} rows");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                })).ToArray();

                for (int i = 0; i < 20; i++)
                {
                    using var ts = eng.StartTransaction();
                    ts.Insert(Table, [("k", 1000 + i), ("v", "w" + i)]);
                    ts.SaveChanges();
                }

                stop.Cancel();
                Assert.IsTrue(Task.WaitAll(readers, 30000), "读者未在预期时间内结束");
                Assert.IsTrue(errors.IsEmpty, "并发读期间出现异常：" + string.Join(" | ", errors.Select(DhSupport.Short)));

                using var check = eng.StartTransaction();
                Assert.AreEqual(70, RowScan.Count(check, Table));
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        // ---------------------------------------------------------------- 隔离与生命周期

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_ReadonlyTransaction_DoesNotSeeUncommittedWriter(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                using var writer = eng.StartTransaction();
                writer.Insert(Table, [("k", 8), ("v", "uncommitted")]);

                using (var ro = eng.StartTransactionReadonly())
                    Assert.IsFalse(ro.Find(Table, "k", 8).IsSuccess, "只读事务不得看到未提交数据");

                writer.SaveChanges();

                using (var ro = eng.StartTransactionReadonly())
                    Assert.IsTrue(ro.Find(Table, "k", 8).IsSuccess, "提交之后新只读事务必须看到该行");
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_EngineDisposeWithLiveTransaction_WaitsThenFails_ThenTransactionDisposesCleanly(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            string? disposeEngineEx = null;
            string? disposeTxEx = null;
            string? reopenEx = null;
            try
            {
                SeedCommitted(eng, rows: 1);

                var tx = eng.StartTransaction();
                tx.Insert(Table, [("k", 5), ("v", "pending")]);
                eng.TimeoutMilliseconds = 100;

                // 有活事务时引擎 Dispose 必须超时失败，而不是把事务悄悄留成孤儿。
                try { eng.Dispose(); }
                catch (Exception ex) { disposeEngineEx = DhSupport.Short(ex); }

                try { tx.Dispose(); }
                catch (Exception ex) { disposeTxEx = DhSupport.Short(ex); }

                try { eng.Dispose(); }
                catch (Exception ex) { reopenEx = DhSupport.Short(ex); }

                DhSupport.Evidence($"引擎 Dispose 与活事务的交互（{backend}）",
                    ("有活事务时 eng.Dispose 异常", disposeEngineEx),
                    ("随后 tx.Dispose 异常", disposeTxEx),
                    ("再次 eng.Dispose 异常", reopenEx));

                Assert.IsNotNull(disposeEngineEx, "有活事务时引擎 Dispose 必须报错");
                Assert.IsNull(disposeTxEx, $"事务本身应能正常释放：{disposeTxEx}");
                Assert.IsNull(reopenEx, $"事务结束后引擎应能正常释放：{reopenEx}");
            }
            finally
            {
                try { eng.Dispose(); } catch { }
                if (backend == TestBackend.File && !string.IsNullOrEmpty(path) && File.Exists(path))
                    try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public void Scenario_TransactionAfterEngineDisposed_IsRejected()
        {
            var eng = new DbEngine();
            SeedCommitted(eng, rows: 1);
            eng.Dispose();

            var writeEx = DhSupport.Capture(() => { using var ts = eng.StartTransaction(); });
            var readEx = DhSupport.Capture(() => { using var ro = eng.StartTransactionReadonly(); });

            Assert.IsTrue(writeEx is LumException, $"引擎释放后开写事务必须被拒绝：{DhSupport.Describe(writeEx)}");
            Assert.IsTrue(readEx is LumException, $"引擎释放后开只读事务必须被拒绝：{DhSupport.Describe(readEx)}");
        }

        [TestMethod]
        public void Scenario_EngineDispose_IsIdempotent_AndKeepsRejectingTransactions()
        {
            var eng = new DbEngine();
            SeedCommitted(eng, rows: 1);
            eng.Dispose();

            // 重复 Dispose 必须是空操作（宿主/DI 容器经常重复释放）。
            var second = DhSupport.Capture(eng.Dispose);
            var third = DhSupport.Capture(eng.Dispose);
            Assert.IsNull(second, $"第二次 Dispose 不得抛异常：{DhSupport.Describe(second)}");
            Assert.IsNull(third, $"第三次 Dispose 不得抛异常：{DhSupport.Describe(third)}");

            var reopen = DhSupport.Capture(() => { using var ts = eng.StartTransaction(); });
            Assert.IsTrue(reopen is LumException, $"释放后的引擎仍必须拒绝事务：{DhSupport.Describe(reopen)}");
        }

        // ---------------------------------------------------------------- 维护操作

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_Vacuum_KeepsRows_AndRejectsCallInsideTransaction(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 200);

                using (var ts = eng.StartTransaction())
                {
                    for (int i = 0; i < 50; i++) ts.Delete(Table, "k", (uint)i);
                    ts.SaveChanges();
                }

                eng.Vacuum();

                using (var check = eng.StartTransaction())
                    Assert.AreEqual(150, RowScan.Count(check, Table), "Vacuum 之后存活行数必须不变");

                using var holder = eng.StartTransaction();
                var nested = DhSupport.Capture(eng.Vacuum);
                Assert.IsTrue(nested is LumException, $"在事务内部调用 Vacuum 必须被拒绝：{DhSupport.Describe(nested)}");
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_AddDropColumn_Commit_Reopen(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 3);

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.AddColumn(Table, "extra", DbValueType.Int, false, 7).IsSuccess);
                    Assert.IsTrue(ts.DropColumn(Table, "v").IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    var names = check.GetTableNames().Values.Single(v => v.tableName == Table).columns;
                    Assert.AreEqual(2, names.Length, "列数应为 k + extra");
                    Assert.AreEqual("extra", names[1].columnName);
                    Assert.AreEqual(7, check.Find(Table, "k", 0).Row.GetInt(1));
                    Assert.AreEqual(3, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_PoolSlots_AreRecycled_OverManyTransactions(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                for (int i = 0; i < 200; i++)
                {
                    using var ts = eng.StartTransaction();
                    ts.Find(Table, "k", 0);
                }

                var holders = new List<ITransactionReadonly>();
                try
                {
                    for (int i = 0; i < DbEngine.MaxConcurrentTransactions; i++)
                        holders.Add(eng.StartTransactionReadonly());
                }
                finally
                {
                    foreach (var h in holders) h.Dispose();
                }

                using (var ok = eng.StartTransaction())
                    Assert.IsTrue(ok.Find(Table, "k", 0).IsSuccess, "槽位回收后事务必须仍然可用");
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        // ---------------------------------------------------------------- 两套 API 一致性

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_EntityApiAndCellApi_SeeTheSameRows(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create<ScnEntity>(Table);
                    ts.Insert(Table, new ScnEntity { Code = 1, Name = "n1", Note = "note-1" });
                    ts.SaveChanges();
                }

                using (var ts = eng.StartTransaction())
                {
                    // Entity 写入 -> Cell 读取
                    var byCell = ts.Find(Table, "Code", 1);
                    Assert.IsTrue(byCell.IsSuccess);
                    Assert.AreEqual("n1", byCell.Row.GetString(1));

                    // Cell 写入 -> Entity 读取
                    Assert.IsTrue(ts.Insert(Table, [("Code", 2), ("Name", "n2"), ("Note", "note-2")]).IsSuccess);
                    var byEntity = ts.FindEntity<ScnEntity>(Table, "Code", 2);
                    Assert.IsTrue(byEntity.IsSuccess);
                    Assert.AreEqual("note-2", byEntity.Value.Note);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsTrue(check.FindEntity<ScnEntity>(Table, "Code", 1).IsSuccess);
                    Assert.IsTrue(check.FindEntity<ScnEntity>(Table, "Code", 2).IsSuccess);
                    Assert.AreEqual(2, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_CommitTwiceWithinOneTransaction_BothBatchesLand(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var setup = eng.StartTransaction())
                {
                    CreateTable(setup);
                    setup.SaveChanges();
                }

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 1), ("v", "first")]).IsSuccess);
                    ts.SaveChanges();

                    // 提交后继续在同一事务里写，再提交一次：两批都要落库。
                    Assert.IsTrue(ts.Find(Table, "k", 1).IsSuccess, "提交后同一事务必须能读到刚提交的行");
                    Assert.IsTrue(ts.Insert(Table, [("k", 2), ("v", "second")]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual("first", check.Find(Table, "k", 1).Row.GetString(1));
                    Assert.AreEqual("second", check.Find(Table, "k", 2).Row.GetString(1));
                    Assert.AreEqual(2, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_DiscardTwice_IsIdempotent_AndTransactionStaysUsable(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 5), ("v", "dropped")]).IsSuccess);
                    ts.Discard();
                    ts.Discard(); // 第二次 Discard 必须是无操作，而不是抛异常
                    Assert.AreEqual(1, RowScan.Count(ts, Table), "两次 Discard 之后工作集仍应等于已提交镜像");

                    Assert.IsTrue(ts.Insert(Table, [("k", 6), ("v", "kept")]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsFalse(check.Find(Table, "k", 5).IsSuccess);
                    Assert.AreEqual("kept", check.Find(Table, "k", 6).Row.GetString(1));
                    Assert.AreEqual(2, RowScan.Count(check, Table));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_EmptyCommit_And_NoOpCommit_AreHarmless(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 2);

                for (int i = 0; i < 5; i++)
                {
                    using var ts = eng.StartTransaction();
                    ts.SaveChanges();          // 无脏页
                    ts.SaveChanges();          // 重复提交
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                    Assert.AreEqual(2, RowScan.Count(check, Table)));
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }
    }
}