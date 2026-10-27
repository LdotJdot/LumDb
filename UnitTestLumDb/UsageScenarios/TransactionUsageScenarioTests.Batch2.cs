using System.Collections.Concurrent;
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
    /// 使用场景覆盖（第二批）：批量数据、跨页大字段、并发写入者、运维动作与
    /// 引擎生命周期边界。每个用例都从调用方视角断言“提交后重开还能读到什么”，
    /// 而不是逐个方法做白盒测试。
    /// </summary>
    public partial class TransactionUsageScenarioTests
    {
        private const string Table2 = "scn2";

        private static bool IsTimeout(Exception? ex) =>
            ex is LumException && ex.Message.StartsWith(LumExceptionMessage.TransactionTimeout, StringComparison.Ordinal);

        /// <summary>
        /// 中途校验：File 后端关闭并重开（真正的落盘验证），Memory 后端直接复用同一实例。
        /// 与 <see cref="Configuration.VerifyCommitted"/> 的区别是它不销毁数据库文件，可以继续写。
        /// </summary>
        private static DbEngine CheckAndReopen(TestBackend backend, DbEngine live, string? path, Action<ITransaction> assert)
        {
            if (backend == TestBackend.Memory)
            {
                using var ts = live.StartTransaction();
                assert(ts);
                return live;
            }

            live.Dispose();
            var reopened = new DbEngine(path!);
            using (var ts = reopened.StartTransaction())
                assert(ts);
            return reopened;
        }

        [LumEntity]
        public partial class ScnEntity2
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            [Str32B] public string Name { get; set; } = "";
            [StrVar] public string Note { get; set; } = "";
        }

        // ---------------------------------------------------------------- 多表 / 批量

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_TwoTables_OneTransaction_CommitAndReopen(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    Assert.IsTrue(ts.Create(Table2, [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]).IsSuccess);
                    Assert.IsTrue(ts.Insert(Table, [("k", 1), ("v", "a1")]).IsSuccess);
                    Assert.IsTrue(ts.Insert(Table, [("k", 2), ("v", "a2")]).IsSuccess);
                    Assert.IsTrue(ts.Insert(Table2, [("k", 11), ("v", "b11")]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    var names = check.GetTableNames().Values.Select(v => v.tableName).ToArray();
                    CollectionAssert.AreEquivalent(new[] { Table, Table2 }, names, "一个事务里建的两张表必须同时可见");
                    Assert.AreEqual(2, RowScan.Count(check, Table));
                    Assert.AreEqual(1, RowScan.Count(check, Table2));
                    Assert.AreEqual("a2", check.Find(Table, "k", 2).Row.GetString(1));
                    Assert.AreEqual("b11", check.Find(Table2, "k", 11).Row.GetString(1));
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
        public void Scenario_BulkInsert_1000Rows_CommitReopenAndUpdate(TestBackend backend)
        {
            const int Rows = 1000;
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    for (int i = 0; i < Rows; i++)
                        Assert.IsTrue(ts.Insert(Table, [("k", i), ("v", "row-" + i)]).IsSuccess, $"insert {i}");
                    ts.SaveChanges();
                }

                eng = CheckAndReopen(backend, eng, path, check =>
                {
                    Assert.AreEqual(Rows, RowScan.Count(check, Table), "批量插入的行数必须完整落库");
                    Assert.AreEqual("row-0", check.Find(Table, "k", 0).Row.GetString(1));
                    Assert.AreEqual("row-500", check.Find(Table, "k", 500).Row.GetString(1));
                    Assert.AreEqual("row-999", check.Find(Table, "k", Rows - 1).Row.GetString(1));
                });

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Update(Table, "k", Rows - 1, "v", "tail-updated").IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(Rows, RowScan.Count(check, Table));
                    Assert.AreEqual("tail-updated", check.Find(Table, "k", Rows - 1).Row.GetString(1));
                    Assert.AreEqual("row-0", check.Find(Table, "k", 0).Row.GetString(1), "更新尾行不得影响首行");
                });

                DhSupport.Evidence($"使用场景：单事务批量插入（{backend}）",
                    ("行数", Rows), ("提交方式", "单事务一次 SaveChanges"), ("重启后校验", "通过"));
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_LargeVarValue_GrowThenShrink_CommitAndReopen(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 6);
                var big = new string('x', 8000) + "|END";

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Update(Table, "k", 3, "v", big).IsSuccess);
                    ts.SaveChanges();
                }

                eng = CheckAndReopen(backend, eng, path, check =>
                {
                    var row = check.Find(Table, "k", 3);
                    Assert.IsTrue(row.IsSuccess, "大字段更新后必须仍可按 key 命中");
                    var s = row.Row.GetString(1);
                    Assert.AreEqual(big.Length, s.Length, "跨页大字段长度必须完整");
                    Assert.IsTrue(s.StartsWith("xxx", StringComparison.Ordinal));
                    Assert.IsTrue(s.EndsWith("|END", StringComparison.Ordinal));
                    Assert.AreEqual("v2", check.Find(Table, "k", 2).Row.GetString(1), "相邻行不得被大字段覆盖");
                    Assert.AreEqual(6, RowScan.Count(check, Table));
                });

                // 再缩回短值：旧的 DataVar 页必须释放，不能留下脏引用。
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Update(Table, "k", 3, "v", "tiny").IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual("tiny", check.Find(Table, "k", 3).Row.GetString(1));
                    Assert.AreEqual("v4", check.Find(Table, "k", 4).Row.GetString(1));
                    Assert.AreEqual(6, RowScan.Count(check, Table));
                });

                DhSupport.Evidence($"使用场景：跨页大字段增长/收缩（{backend}）",
                    ("大字段长度", big.Length), ("增长后重开", "长度与首尾一致"), ("收缩后重开", "tiny"));
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        // ---------------------------------------------------------------- 可见性 / 回滚

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_ReadOwnUncommittedWrites_ThenDiscardRestoresImage(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 3);

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 9), ("v", "temp")]).IsSuccess);
                    Assert.IsTrue(ts.Update(Table, "k", 0, "v", "temp0").IsSuccess);
                    Assert.IsTrue(ts.Delete(Table, "k", 1).IsSuccess);

                    // 事务必须读到自己未提交的写入。
                    Assert.IsTrue(ts.Find(Table, "k", 9).IsSuccess, "事务必须读到自己的插入");
                    Assert.AreEqual("temp0", ts.Find(Table, "k", 0).Row.GetString(1), "事务必须读到自己的更新");
                    Assert.IsFalse(ts.Find(Table, "k", 1).IsSuccess, "事务必须看到自己的删除");
                    Assert.AreEqual(3, RowScan.Count(ts, Table));

                    ts.Discard();
                    Assert.AreEqual(3, RowScan.Count(ts, Table), "Discard 必须回到已提交镜像");
                    Assert.AreEqual("v0", ts.Find(Table, "k", 0).Row.GetString(1));
                    Assert.IsTrue(ts.Find(Table, "k", 1).IsSuccess, "Discard 必须恢复被删除的行");
                    Assert.IsFalse(ts.Find(Table, "k", 9).IsSuccess, "Discard 必须撤销未提交插入");

                    Assert.IsTrue(ts.Insert(Table, [("k", 7), ("v", "after-discard")]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(4, RowScan.Count(check, Table));
                    Assert.AreEqual("v0", check.Find(Table, "k", 0).Row.GetString(1));
                    Assert.AreEqual("v1", check.Find(Table, "k", 1).Row.GetString(1));
                    Assert.IsFalse(check.Find(Table, "k", 9).IsSuccess);
                    Assert.AreEqual("after-discard", check.Find(Table, "k", 7).Row.GetString(1));
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
        public void Scenario_DeleteAllRows_ThenReinsert_TableStaysUsable(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 50);

                using (var ts = eng.StartTransaction())
                {
                    var keys = RowScan.ToObjectRows(ts, Table).Select(r => (int)r[0]).ToList();
                    Assert.AreEqual(50, keys.Count);
                    foreach (var key in keys)
                        Assert.IsTrue(ts.Delete(Table, "k", key).IsSuccess, $"delete {key}");

                    Assert.AreEqual(0, RowScan.Count(ts, Table), "删空后表内行数必须为 0");
                    for (int i = 0; i < 5; i++)
                        Assert.IsTrue(ts.Insert(Table, [("k", 1000 + i), ("v", "new-" + i)]).IsSuccess);
                    ts.SaveChanges();
                }

                eng = CheckAndReopen(backend, eng, path, check =>
                {
                    Assert.AreEqual(5, RowScan.Count(check, Table), "删空后重新插入的行必须落库");
                    Assert.IsFalse(check.Find(Table, "k", 0).IsSuccess, "旧行不得残留");
                    Assert.AreEqual("new-4", check.Find(Table, "k", 1004).Row.GetString(1));
                });

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 2000), ("v", "third-life")]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(6, RowScan.Count(check, Table));
                    Assert.AreEqual("third-life", check.Find(Table, "k", 2000).Row.GetString(1));
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
        public void Scenario_DeleteThenInsertSameKey_InOneTransaction(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 1);

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Delete(Table, "k", 0).IsSuccess);
                    Assert.IsTrue(ts.Insert(Table, [("k", 0), ("v", "reborn")]).IsSuccess,
                        "删除同事务内应释放唯一键，允许立即复用");
                    ts.SaveChanges();
                }

                // 复用之后唯一约束必须仍然有效。
                var dup = DhSupport.Capture(() =>
                {
                    using var ts = eng.StartTransaction();
                    ts.Insert(Table, [("k", 0), ("v", "dup")]);
                });
                Assert.IsNotNull(dup, "删除+复用之后重复键仍必须被拒绝");

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(1, RowScan.Count(check, Table));
                    Assert.AreEqual("reborn", check.Find(Table, "k", 0).Row.GetString(1));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        // ---------------------------------------------------------------- 并发写入者

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_ConcurrentWriters_DisjointRows_AllPersisted(TestBackend backend)
        {
            const int Writers = 4;
            const int PerWriter = 10;
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 3);
                var errors = new ConcurrentQueue<Exception>();

                var tasks = Enumerable.Range(0, Writers).Select(w => Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < PerWriter; i++)
                        {
                            using var ts = eng.StartTransaction();
                            Assert.IsTrue(ts.Insert(Table, [("k", 1000 + w * 100 + i), ("v", $"w{w}-{i}")]).IsSuccess);
                            ts.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                })).ToArray();

                Assert.IsTrue(Task.WaitAll(tasks, 60000), "并发写者未在 60s 内结束");
                Assert.IsTrue(errors.IsEmpty, "并发提交出现异常：" + string.Join(" | ", errors.Select(DhSupport.Short)));

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(3 + Writers * PerWriter, RowScan.Count(check, Table), "并发写者的行必须全部落库");
                    Assert.AreEqual("w0-0", check.Find(Table, "k", 1000).Row.GetString(1));
                    Assert.AreEqual("w3-9", check.Find(Table, "k", 1309).Row.GetString(1));
                });

                DhSupport.Evidence($"使用场景：并发写入者（{backend}）",
                    ("写者", Writers), ("每个写者事务数", PerWriter), ("异常", errors.Count),
                    ("最终行数", 3 + Writers * PerWriter));
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_LongLivedReader_DelaysCommit_TimeoutThenRetrySucceeds(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 5);
                eng.TimeoutMilliseconds = 200;

                // 读者必须在另一条线程上打开：同线程已有事务时开写事务会被设计性拒绝
                // （Scenario_WriteTransactionOnThreadWithLiveReader_IsRejected 固化了这条约束）。
                ITransactionReadonly? reader = null;
                var readerThread = new Thread(() => reader = eng.StartTransactionReadonly());
                readerThread.Start();
                Assert.IsTrue(readerThread.Join(10000), "读者线程未启动");
                Assert.IsNotNull(reader);
                try
                {
                    using (var writer = eng.StartTransaction())
                    {
                        Assert.IsTrue(writer.Insert(Table, [("k", 42), ("v", "pending")]).IsSuccess);

                        // 读者持锁期间提交必须按超时报错，而不是无限等待。
                        var commitEx = DhSupport.Capture(writer.SaveChanges);
                        Assert.IsTrue(IsTimeout(commitEx), $"读者存活时提交必须先超时失败：{DhSupport.Describe(commitEx)}");
                        Assert.IsFalse(reader.Find(Table, "k", 42).IsSuccess, "未提交数据对读者不可见");

                        reader.Dispose();
                        var retryEx = DhSupport.Capture(writer.SaveChanges);
                        Assert.IsNull(retryEx, "读者释放后同一事务重试提交必须成功（脏页不得因超时丢失）");

                        DhSupport.Evidence($"使用场景：长活读者阻塞提交（{backend}）",
                            ("首次提交异常", DhSupport.Describe(commitEx)),
                            ("锁等待超时(ms)", eng.TimeoutMilliseconds),
                            ("读者释放后重试", retryEx == null ? "成功" : DhSupport.Describe(retryEx)));
                    }
                }
                finally
                {
                    reader.Dispose(); // 幂等
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(6, RowScan.Count(check, Table));
                    Assert.AreEqual("pending", check.Find(Table, "k", 42).Row.GetString(1));
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
        public void Scenario_PoolWait_NewTransactionStartsAfterSlotFreed(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            var holders = new List<ITransactionReadonly>();
            try
            {
                SeedCommitted(eng, rows: 1);
                eng.TransactionPoolWaitMilliseconds = 2000;

                for (int i = 0; i < DbEngine.MaxConcurrentTransactions; i++)
                    holders.Add(eng.StartTransactionReadonly());

                var sw = Stopwatch.StartNew();
                var probe = Task.Run(() =>
                {
                    using var ro = eng.StartTransactionReadonly();
                    return ro.Find(Table, "k", 0).IsSuccess;
                });

                Thread.Sleep(150);
                Assert.IsFalse(probe.IsCompleted, "池满时新事务必须等待，而不是立刻失败或立刻成功");

                holders[0].Dispose();
                holders.RemoveAt(0);

                Assert.IsTrue(probe.Wait(10000), "释放槽位后等待中的事务必须成功启动");
                Assert.IsTrue(probe.Result);
                Assert.IsTrue(sw.ElapsedMilliseconds >= 100, "探针事务不应在槽位释放前完成");

                foreach (var h in holders) h.Dispose();
                holders.Clear();

                using (var ok = eng.StartTransaction())
                    Assert.IsTrue(ok.Find(Table, "k", 0).IsSuccess, "槽位回收后事务必须仍然可用");

                DhSupport.Evidence($"使用场景：事务池等待（{backend}）",
                    ("并发上限", DbEngine.MaxConcurrentTransactions),
                    ("池满后等待(ms)", 150),
                    ("槽位释放后探针耗时(ms)", sw.ElapsedMilliseconds),
                    ("探针结果", probe.Result ? "成功" : "失败"));
            }
            finally
            {
                foreach (var h in holders) h.Dispose();
                eng.TransactionPoolWaitMilliseconds = 0;
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        public void Scenario_WriteTransactionOnThreadWithLiveReader_IsRejected()
        {
            using var eng = new DbEngine();
            SeedCommitted(eng, rows: 1);

            using var reader = eng.StartTransactionReadonly();
            var ex = DhSupport.Capture(() =>
            {
                using var writer = eng.StartTransaction();
            });

            Assert.IsTrue(ex is LumException,
                $"同线程已有事务（含只读）时开写事务必须被拒绝：{DhSupport.Describe(ex)}");
            Assert.IsTrue(reader.Find(Table, "k", 0).IsSuccess, "被拒绝的写事务不得影响已打开的读者");
        }

        // ---------------------------------------------------------------- 重启 / 备份

        [TestMethod]
        public void Scenario_ReopenLoop_CommittedDataSurvivesFiveRestarts()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                for (int cycle = 0; cycle < 5; cycle++)
                {
                    using var eng = new DbEngine(path);
                    using var ts = eng.StartTransaction();
                    if (cycle == 0) CreateTable(ts);
                    Assert.IsTrue(ts.Insert(Table, [("k", 100 + cycle), ("v", "cycle-" + cycle)]).IsSuccess);
                    ts.SaveChanges();
                }

                using (var eng = new DbEngine(path))
                using (var check = eng.StartTransaction())
                {
                    Assert.AreEqual(5, RowScan.Count(check, Table), "五次重启后行数必须保持");
                    for (int cycle = 0; cycle < 5; cycle++)
                        Assert.AreEqual("cycle-" + cycle, check.Find(Table, "k", 100 + cycle).Row.GetString(1),
                            $"第 {cycle} 轮的数据必须在重启后仍可读");
                }

                DhSupport.Evidence("使用场景：五次关闭/重开循环",
                    ("重启次数", 5), ("最终行数", 5), ("每轮写入", "1 行 + SaveChanges"));
            }
            finally
            {
                DhSupport.TryDelete(path, path + ".log");
            }
        }

        [TestMethod]
        public void Scenario_SaveToBackup_ReopensCommittedImage()
        {
            using var eng = new DbEngine();
            var backup = Configuration.GetRandomPath();
            try
            {
                SeedCommitted(eng, rows: 4);
                eng.SaveTo(backup);

                using (var bak = new DbEngine(backup))
                using (var check = bak.StartTransaction())
                {
                    Assert.AreEqual(4, RowScan.Count(check, Table), "备份必须是完整的已提交镜像");
                    Assert.AreEqual("v3", check.Find(Table, "k", 3).Row.GetString(1));
                }

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 99), ("v", "later")]).IsSuccess);
                    ts.SaveChanges();
                }

                // 第二次 SaveTo 必须覆盖旧备份（而不是追加）。
                eng.SaveTo(backup);

                using (var bak = new DbEngine(backup))
                using (var check = bak.StartTransaction())
                {
                    Assert.AreEqual(5, RowScan.Count(check, Table));
                    Assert.AreEqual("later", check.Find(Table, "k", 99).Row.GetString(1));
                }

                using (var ts = eng.StartTransaction())
                    Assert.IsTrue(ts.Find(Table, "k", 99).IsSuccess, "SaveTo 不得影响源引擎");
            }
            finally
            {
                try { eng.Dispose(); } catch { }
                DhSupport.TryDelete(backup);
            }
        }

        // ---------------------------------------------------------------- 按行 id / 实体 API

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Scenario_GoThroughRowIds_UpdateAndDeleteById(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedCommitted(eng, rows: 10);

                using (var ts = eng.StartTransaction())
                {
                    var pairs = new List<(uint id, int k)>();
                    ts.GoThrough(Table, (uint id, ref RowView row) =>
                    {
                        pairs.Add((id, row.GetInt(0)));
                        return true;
                    });
                    Assert.AreEqual(10, pairs.Count, "按 id 扫描必须覆盖整表");

                    foreach (var (id, k) in pairs.Where(p => p.k is 3 or 5))
                        Assert.IsTrue(ts.Update(Table, id, "v", "id-updated-" + k).IsSuccess, $"update id {id}");

                    var toDelete = pairs.Single(p => p.k == 4);
                    Assert.IsTrue(ts.Delete(Table, toDelete.id).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(9, RowScan.Count(check, Table));
                    Assert.AreEqual("id-updated-3", check.Find(Table, "k", 3).Row.GetString(1));
                    Assert.AreEqual("id-updated-5", check.Find(Table, "k", 5).Row.GetString(1));
                    Assert.IsFalse(check.Find(Table, "k", 4).IsSuccess);
                    Assert.AreEqual("v9", check.Find(Table, "k", 9).Row.GetString(1));
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
        public void Scenario_EntityPaging_Count_AndPredicateDelete(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Create<ScnEntity2>(Table).IsSuccess);
                    for (int i = 0; i < 20; i++)
                        Assert.IsTrue(ts.Insert(Table, new ScnEntity2 { Code = i, Name = "n" + i, Note = "note-" + i }).IsSuccess);
                    ts.SaveChanges();
                }

                using (var ts = eng.StartTransaction())
                {
                    var page = ts.Find<ScnEntity2>(Table, x => x.Code >= 5, skip: 2, limit: 3);
                    CollectionAssert.AreEqual(new[] { 7, 8, 9 }, page.Values.Select(x => x.Code).ToArray(),
                        "分页必须落在正确的窗口");

                    Assert.AreEqual(10u, ts.Count<ScnEntity2>(Table, x => x.Code >= 10).Value);

                    var deleted = ts.Delete<ScnEntity2>(Table, x => x.Code < 10);
                    Assert.AreEqual(10u, deleted.Value, "谓词删除必须返回删除行数");
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(10, RowScan.Count(check, Table));
                    Assert.IsFalse(check.Find(Table, "Code", 0).IsSuccess);
                    Assert.IsTrue(check.Find(Table, "Code", 10).IsSuccess);
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
        public void Scenario_EntityUpdateAndDeleteById_CommitAndReopen(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create<ScnEntity2>(Table);
                    for (int code = 10; code < 15; code++)
                        Assert.IsTrue(ts.Insert(Table, new ScnEntity2 { Code = code, Name = "n" + code, Note = "note-" + code }).IsSuccess);

                    var ids = new Dictionary<int, uint>();
                    ts.GoThrough(Table, (uint id, ref RowView row) =>
                    {
                        ids[row.GetInt(0)] = id;
                        return true;
                    });
                    Assert.AreEqual(5, ids.Count);

                    var byId = ts.FindByIdEntity<ScnEntity2>(Table, ids[12]);
                    Assert.IsTrue(byId.IsSuccess);
                    Assert.AreEqual("n12", byId.Value.Name);

                    Assert.IsTrue(ts.UpdateEntity(Table, ids[12],
                        new ScnEntity2 { Code = 12, Name = "n12b", Note = "note-12b" }).IsSuccess);
                    Assert.IsTrue(ts.Delete(Table, ids[14]).IsSuccess);
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.AreEqual(4, RowScan.Count(check, Table));
                    var updated = check.FindEntity<ScnEntity2>(Table, "Code", 12);
                    Assert.IsTrue(updated.IsSuccess);
                    Assert.AreEqual("n12b", updated.Value.Name);
                    Assert.AreEqual("note-12b", updated.Value.Note);
                    Assert.IsFalse(check.Find(Table, "Code", 14).IsSuccess);
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        // ---------------------------------------------------------------- 日志文件契约

        [TestMethod]
        public void Scenario_EmptyCommit_LeavesNoLogFile()
        {
            var eng = Configuration.Create(TestBackend.File, out var path);
            var logPath = path + ".log";
            try
            {
                SeedCommitted(eng, rows: 2);
                Assert.IsFalse(File.Exists(logPath), "正常提交结束后不得残留 .log");

                for (int i = 0; i < 5; i++)
                {
                    using var ts = eng.StartTransaction();
                    ts.SaveChanges(); // 无脏页
                }

                Assert.IsFalse(File.Exists(logPath), "无脏页的 SaveChanges 不得创建 .log");
            }
            finally
            {
                Configuration.Cleanup(TestBackend.File, eng, path);
            }
        }

        [TestMethod]
        public void Scenario_EmptyCommit_WithLogPathBlocked_DoesNotThrow()
        {
            var eng = Configuration.Create(TestBackend.File, out var path);
            var hostage = path + ".log";
            try
            {
                SeedCommitted(eng, rows: 1);

                // 用目录占位让 .log 无法创建；无脏页提交不应触碰日志路径。
                Directory.CreateDirectory(hostage);
                for (int i = 0; i < 3; i++)
                {
                    var ex = DhSupport.Capture(() =>
                    {
                        using var ts = eng.StartTransaction();
                        ts.SaveChanges();
                    });
                    Assert.IsNull(ex, $"无脏页提交不得因日志路径不可用而失败：{DhSupport.Describe(ex)}");
                }

                DhSupport.Evidence("使用场景：日志路径被目录占位时的空提交",
                    ("空提交次数", 3), ("异常", "无"), ("日志文件存在", File.Exists(hostage)));

                using (var ro = eng.StartTransactionReadonly())
                    Assert.IsTrue(ro.Find(Table, "k", 0).IsSuccess, "只读事务不受日志路径影响");

                Directory.Delete(hostage, true);

                // 日志路径恢复后，真实提交必须正常。
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Insert(Table, [("k", 2), ("v", "after-unblock")]).IsSuccess);
                    ts.SaveChanges();
                }

                using (var check = eng.StartTransaction())
                    Assert.AreEqual(2, RowScan.Count(check, Table));
            }
            finally
            {
                DhSupport.TryDelete(hostage);
                Configuration.Cleanup(TestBackend.File, eng, path);
            }
        }
    }
}