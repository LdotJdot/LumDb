using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.DefectHunt
{
    /// <summary>
    /// 事务语义 / 文件识别的修复验证用例。
    /// DH4: DbCell API 的失败语句 -> 事务作废（不再静默 Reset 后继续提交）。
    /// DH5: Entity API 与 DbCell API 语义一致。
    /// DH6: TransactionPolicy 非法值被拒绝；Serializable 在启动事务时显式不支持。
    /// DH8: 文件签名 / 页面格式版本受校验，损坏文件被受控拒绝。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public partial class DefectHunt_TransactionSemanticsTests
    {
        private const string Table = "dh";

        [LumEntity]
        public partial class DhEntity
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            [Str32B] public string Name { get; set; } = "";
        }

        private static void SeedTable(DbEngine eng)
        {
            using var ts = eng.StartTransaction();
            ts.Create(Table, [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
            ts.Insert(Table, [("k", 1), ("v", "committed-A")]);
            ts.SaveChanges();
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void DH4_FailedStatement_AbortsTransaction_DbCellApi(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                SeedTable(eng);

                var tx = eng.StartTransaction();
                var faults = new List<string>();
                Exception? duplicateEx = null;
                Exception? nextStatementEx = null;
                Exception? commitEx = null;
                bool faulted = false;

                try
                {
                    Assert.IsTrue(tx.Insert(Table, [("k", 2), ("v", "precious-B")]).IsSuccess);

                    duplicateEx = DhSupport.Capture(() => tx.Insert(Table, [("k", 2), ("v", "duplicate")]));
                    faulted = ((LumTransaction)tx).IsFaulted;

                    // 作废后任何语句都必须被拒绝，而不是返回 IsSuccess=True 却写不进库。
                    nextStatementEx = DhSupport.Capture(() => tx.Insert(Table, [("k", 3), ("v", "C")]));
                    commitEx = DhSupport.Capture(tx.SaveChanges);
                }
                finally
                {
                    DhSupport.TryDelete();
                    try { tx.Dispose(); } catch (Exception ex) { faults.Add(DhSupport.Short(ex)); }
                }

                bool b, c;
                int rows;
                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    b = check.Find(Table, "k", 2).IsSuccess;
                    c = check.Find(Table, "k", 3).IsSuccess;
                    rows = RowScan.Count(check, Table);
                    Assert.IsFalse(b, "失败事务中更早的插入不得落库");
                    Assert.IsFalse(c, "失败事务中的后续插入不得落库");
                    Assert.AreEqual(1, rows, "只有已提交的 A 行存在");
                    Assert.IsTrue(check.Find(Table, "k", 1).IsSuccess);
                });

                DhSupport.Evidence($"DH4 失败语句作废整个事务（{backend}）",
                    ("重复键异常", DhSupport.Describe(duplicateEx)),
                    ("事务是否被标记作废", faulted),
                    ("作废后继续插入的异常", DhSupport.Describe(nextStatementEx)),
                    ("作废后 SaveChanges 的异常", DhSupport.Describe(commitEx)),
                    ("Dispose 异常", string.Join(" | ", faults)));

                Assert.IsNotNull(duplicateEx, "重复键必须抛出");
                StringAssert.StartsWith(duplicateEx!.Message, LumExceptionMessage.KeyAlreadyExisted);
                Assert.IsTrue(faulted, "失败语句之后事务必须处于作废状态");
                Assert.IsTrue(DhSupport.IsAborted(nextStatementEx),
                    $"作废后继续执行语句必须在到达存储层前被拒绝，实际：{DhSupport.Describe(nextStatementEx)}");
                Assert.IsTrue(DhSupport.IsAborted(commitEx),
                    $"作废后 SaveChanges 必须拒绝提交，实际：{DhSupport.Describe(commitEx)}");
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void DH5_FailedStatement_AbortsTransaction_EntityApi_MatchesDbCellApi(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var seed = eng.StartTransaction())
                {
                    seed.Create<DhEntity>(Table);
                    seed.Insert(Table, new DhEntity { Code = 1, Name = "committed-A" });
                    seed.SaveChanges();
                }

                var tx = eng.StartTransaction();
                Exception? duplicateEx = null;
                bool faulted;
                Exception? commitEx = null;
                try
                {
                    Assert.IsTrue(tx.Insert(Table, new DhEntity { Code = 2, Name = "precious-B" }).IsSuccess);
                    duplicateEx = DhSupport.Capture(() => tx.Insert(Table, new DhEntity { Code = 2, Name = "duplicate" }));
                    faulted = ((LumTransaction)tx).IsFaulted;
                    commitEx = DhSupport.Capture(tx.SaveChanges);
                }
                finally
                {
                    try { tx.Dispose(); } catch { }
                }

                Configuration.VerifyCommitted(backend, eng, path, check =>
                {
                    Assert.IsFalse(check.Find(Table, "Code", 2).IsSuccess, "失败事务中的 B 行不得落库");
                    Assert.IsTrue(check.Find(Table, "Code", 1).IsSuccess, "已提交的 A 行必须存在");
                    Assert.AreEqual(1, RowScan.Count(check, Table));
                });

                DhSupport.Evidence($"DH5 Entity API 与 DbCell API 语义一致（{backend}）",
                    ("重复键异常", DhSupport.Describe(duplicateEx)),
                    ("事务是否被标记作废", faulted),
                    ("作废后 SaveChanges 的异常", DhSupport.Describe(commitEx)));

                Assert.IsNotNull(duplicateEx);
                Assert.IsTrue(faulted, "Entity API 失败后事务也必须作废");
                Assert.IsTrue(DhSupport.IsAborted(commitEx),
                    $"作废后 SaveChanges 必须拒绝提交，实际：{DhSupport.Describe(commitEx)}");
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        public void DH4b_Discard_ReopensFaultedTransaction()
        {
            using var eng = new DbEngine();
            SeedTable(eng);

            var tx = eng.StartTransaction();
            DhSupport.Capture(() => tx.Insert(Table, [("k", 1), ("v", "duplicate")]));
            Assert.IsTrue(((LumTransaction)tx).IsFaulted);

            tx.Discard();
            Assert.IsFalse(((LumTransaction)tx).IsFaulted, "Discard 必须把事务恢复到可用状态");

            Assert.IsTrue(tx.Insert(Table, [("k", 5), ("v", "after-discard")]).IsSuccess);
            tx.SaveChanges();
            tx.Dispose();

            using var check = eng.StartTransaction();
            Assert.IsTrue(check.Find(Table, "k", 5).IsSuccess);
            Assert.AreEqual(2, RowScan.Count(check, Table));
        }

        [TestMethod]
        public void DH6_TransactionPolicy_InvalidValueRejected_SerializableNotSupported()
        {
            using var eng = new DbEngine();

            var invalidEx = DhSupport.Capture(() => eng.TransactionPolicy = (TransactionPolicy)12345);
            Assert.IsNull(DhSupport.Capture(() => eng.TransactionPolicy = TransactionPolicy.Serializable),
                "Serializable 是合法枚举值，赋值本身不应失败");

            var writeEx = DhSupport.Capture(() => { using var ts = eng.StartTransaction(); });
            var readEx = DhSupport.Capture(() => { using var ro = eng.StartTransactionReadonly(); });

            eng.TransactionPolicy = TransactionPolicy.ReadCommitted;
            var okEx = DhSupport.Capture(() => { using var ts = eng.StartTransaction(); });

            DhSupport.Evidence("DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持",
                ("赋值 (TransactionPolicy)12345 的异常", DhSupport.Describe(invalidEx)),
                ("Serializable 下 StartTransaction 的异常", DhSupport.Describe(writeEx)),
                ("Serializable 下 StartTransactionReadonly 的异常", DhSupport.Describe(readEx)),
                ("切回 ReadCommitted 后的异常", DhSupport.Describe(okEx)));

            Assert.IsInstanceOfType<ArgumentOutOfRangeException>(invalidEx, "非法策略值必须在赋值时被拒绝");
            Assert.IsInstanceOfType<NotSupportedException>(writeEx, "Serializable 写事务必须在启动时显式拒绝");
            Assert.IsInstanceOfType<NotSupportedException>(readEx, "Serializable 只读事务也必须在启动时显式拒绝");
            Assert.IsNull(okEx);
        }

        [TestMethod]
        public void DH8_BadFiles_AreRejectedWithActionableErrors()
        {
            var path = DhSupport.TempPath("dh8");
            var magicPath = DhSupport.TempPath("dh8_magic");
            var versionPath = DhSupport.TempPath("dh8_version");
            var truncPath = DhSupport.TempPath("dh8_trunc");
            var garbagePath = DhSupport.TempPath("dh8_garbage");
            try
            {
                SeedTableAt(path);
                var original = File.ReadAllBytes(path);

                // A) 抹掉 6 字节签名 "LumDBS"
                var magic = (byte[])original.Clone();
                for (int i = 0; i < 6; i++) magic[i] = (byte)'X';
                File.WriteAllBytes(magicPath, magic);

                // B) 把 FileVersion 改成 9.9.9
                var version = (byte[])original.Clone();
                BitConverter.GetBytes(9_009_009u).CopyTo(version, 6);
                File.WriteAllBytes(versionPath, version);

                // C) 截断到 50 字节
                File.WriteAllBytes(truncPath, original.Take(50).ToArray());

                // D) 完全无关的垃圾文件
                File.WriteAllBytes(garbagePath, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

                var control = OpenAndRead(path);
                var magicProbe = OpenAndRead(magicPath);
                var versionProbe = OpenAndRead(versionPath);
                var truncProbe = OpenAndRead(truncPath);
                var garbageProbe = OpenAndRead(garbagePath);

                DhSupport.Evidence("DH8 文件签名 / 页面格式版本 / 长度受校验",
                    ("正常文件（对照）", control),
                    ("抹掉签名的文件", magicProbe),
                    ("版本号改写为 9.9.9 的文件", versionProbe),
                    ("截断到 50 字节的文件", truncProbe),
                    ("10 字节垃圾文件", garbageProbe));

                Assert.AreEqual("READ OK: payload", control, "对照文件必须可读");

                StringAssert.Contains(magicProbe, LumExceptionMessage.InvalidDatabaseFile);
                StringAssert.Contains(versionProbe, LumExceptionMessage.UnsupportedFileVersion);
                StringAssert.Contains(versionProbe, "9.9.9");
                StringAssert.Contains(truncProbe, LumExceptionMessage.InvalidDatabaseFile);
                StringAssert.Contains(garbageProbe, LumExceptionMessage.InvalidDatabaseFile);
            }
            finally
            {
                DhSupport.TryDelete(path, path + ".log", magicPath, magicPath + ".log",
                    versionPath, versionPath + ".log", truncPath, truncPath + ".log",
                    garbagePath, garbagePath + ".log");
            }
        }

        private static void SeedTableAt(string path)
        {
            using var eng = new DbEngine(path);
            using var ts = eng.StartTransaction();
            ts.Create(Table, [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
            ts.Insert(Table, [("k", 1), ("v", "payload")]);
        }

        /// <summary>Open the image and try to read k=1. Returns "READ OK: ..." or the rejection text.</summary>
        private static string OpenAndRead(string path)
        {
            try
            {
                using var eng = new DbEngine(path, createIfNotExists: false);
                using var ts = eng.StartTransaction();
                var r = ts.Find(Table, "k", 1);
                return r.IsSuccess ? $"READ OK: {r.Row.GetString(1)}" : "Find 失败（表/行不可见）";
            }
            catch (Exception ex)
            {
                return DhSupport.Short(ex);
            }
        }
    }
}