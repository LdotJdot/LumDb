using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.Transaction
{
    [TestClass]
    public class TransactionDurabilityTests
    {
        const string Table = "dur";

        static void CreateTable(ITransaction ts) =>
            ts.Create(Table, [("sku", DbValueType.Int, true), ("note", DbValueType.StrVar, false)]);

        static void InsertRow(ITransaction ts, int sku, string note) =>
            ts.Insert(Table, [("sku", sku), ("note", note)]);

        static bool TryFind(ITransaction ts, int sku, out string? note) =>
            TryFind(ts.Find(Table, "sku", sku), out note);

        static bool TryFind(ITransactionReadonly ts, int sku, out string? note) =>
            TryFind(ts.Find(Table, "sku", sku), out note);

        static bool TryFind(LumDbEngine.Element.Engine.Results.IDbValue result, out string? note)
        {
            if (!result.IsSuccess)
            {
                note = null;
                return false;
            }
            note = result.Row.GetString(1);
            return true;
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void Dispose_Commits_VisibleInNextTransaction(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "keep");
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsTrue(TryFind(ts, 1, out var note));
                    Assert.AreEqual("keep", note);
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
        public void Discard_ThenDispose_DoesNotCommit(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "drop");
                    ts.Discard();
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsFalse(ts.Find(Table, "sku", 1).IsSuccess);
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
        public void SaveChanges_ThenInsert_ThenDiscard_KeepsOnlyCommitted(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "saved");
                    ts.SaveChanges();
                    InsertRow(ts, 2, "rolled");
                    ts.Discard();
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsTrue(TryFind(ts, 1, out var a));
                    Assert.AreEqual("saved", a);
                    Assert.IsFalse(TryFind(ts, 2, out _));
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
        public void Discard_ThenSameTransaction_FindAndInsert(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "gone");
                    ts.Discard();
                    Assert.IsFalse(TryFind(ts, 1, out _), "Find after Discard must reload the committed image");
                    CreateTable(ts);
                    InsertRow(ts, 2, "ok");
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsFalse(TryFind(ts, 1, out _));
                    Assert.IsTrue(TryFind(ts, 2, out var note));
                    Assert.AreEqual("ok", note);
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
        public void WriteThrow_DoesNotCommitFailedInsert(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "base");
                }

                using (var ts = eng.StartTransaction())
                {
                    try
                    {
                        InsertRow(ts, 1, "dup");
                        Assert.Fail("expected duplicate key");
                    }
                    catch (Exception ex)
                    {
                        Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.KeyAlreadyExisted), ex.Message);
                    }
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsTrue(TryFind(ts, 1, out var note));
                    Assert.AreEqual("base", note);
                    Assert.AreEqual(1, RowScan.Count(ts, Table));
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
        public void IsSuccessFalse_DoesNotPolluteCommitted_SameTxContinues(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "keep");
                    var miss = ts.Find("no-such-table", 1u);
                    Assert.IsFalse(miss.IsSuccess);
                    var dup = ts.Create(Table, [("sku", DbValueType.Int, true)]);
                    Assert.IsFalse(dup.IsSuccess);
                    InsertRow(ts, 2, "also");
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsTrue(TryFind(ts, 1, out _));
                    Assert.IsTrue(TryFind(ts, 2, out var n));
                    Assert.AreEqual("also", n);
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
        public void ReadonlySeesLastCommit_NotWriterDirtyPages(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "committed");
                }

                using var writer = eng.StartTransaction();
                InsertRow(writer, 2, "dirty");
                using (var ro = eng.StartTransactionReadonly())
                {
                    Assert.IsTrue(TryFind(ro, 1, out var c));
                    Assert.AreEqual("committed", c);
                    Assert.IsFalse(TryFind(ro, 2, out _), "readonly must not see uncommitted writer pages");
                }
                writer.Discard();
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void SaveTo_OmitsUncommittedWriterPages(TestBackend backend)
        {
            var dump = Configuration.GetRandomPath();
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    InsertRow(ts, 1, "on-disk");
                }

                using var writer = eng.StartTransaction();
                InsertRow(writer, 2, "not-dumped");
                eng.SaveTo(dump);
                writer.Discard();

                using (var file = new DbEngine(dump))
                using (var ts = file.StartTransaction())
                {
                    Assert.IsTrue(TryFind(ts, 1, out var n));
                    Assert.AreEqual("on-disk", n);
                    Assert.IsFalse(TryFind(ts, 2, out _));
                    file.SetDestoryOnDisposed();
                }
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
                if (File.Exists(dump))
                    File.Delete(dump);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void SaveTo_RoundTrip_VarIndexAndHoles(TestBackend backend)
        {
            var dump = Configuration.GetRandomPath();
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    CreateTable(ts);
                    for (int i = 1; i <= 20; i++)
                        InsertRow(ts, i, new string((char)('A' + i % 26), 4000 + i));
                    for (uint id = 1; id <= 20; id += 2)
                        ts.Delete(Table, id);
                }

                int memCount;
                string? sample;
                using (var ts = eng.StartTransaction())
                {
                    memCount = RowScan.Count(ts, Table);
                    Assert.IsTrue(TryFind(ts, 2, out sample));
                    Assert.IsFalse(TryFind(ts, 1, out _));
                }

                eng.SaveTo(dump);

                using (var file = new DbEngine(dump))
                using (var ts = file.StartTransaction())
                {
                    Assert.AreEqual(memCount, RowScan.Count(ts, Table));
                    Assert.IsTrue(TryFind(ts, 2, out var dumped));
                    Assert.AreEqual(sample, dumped);
                    Assert.IsFalse(TryFind(ts, 1, out _));
                    file.SetDestoryOnDisposed();
                }
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
                if (File.Exists(dump))
                    File.Delete(dump);
            }
        }
    }
}
