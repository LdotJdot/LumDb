using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.Transaction
{
    [TestClass]
    public class MemoryEngineExtraTests
    {
        [TestMethod]
        public void Version_PackedAndDotted()
        {
            using var eng = new DbEngine();
            Assert.AreEqual(1_003_009u, eng.Version);
            Assert.AreEqual("1.3.9", eng.VersionString);
            Assert.AreEqual("1.3.9", DbHeader.FormatVersion(DbHeader.VERSION));
        }

        [TestMethod]
        public void EmptyMemory_SaveTo_OpensAsFile()
        {
            var dump = Configuration.GetRandomPath();
            try
            {
                using (var mem = new DbEngine())
                    mem.SaveTo(dump);

                using var file = new DbEngine(dump);
                Assert.AreEqual("1.3.9", file.VersionString);
                using var ts = file.StartTransaction();
                Assert.AreEqual(0, ts.GetTableNames().Values.Count);
                file.SetDestoryOnDisposed();
            }
            finally
            {
                if (File.Exists(dump)) File.Delete(dump);
            }
        }

        [TestMethod]
        public void GoThroughId_MatchesFindById_AndUpdateDelete()
        {
            using var eng = new DbEngine();
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("sku", DbValueType.Int, true), ("note", DbValueType.Str32B, false)]);
                ts.Insert("t", [("sku", 10), ("note", "a")]);
                ts.Insert("t", [("sku", 20), ("note", "b")]);
            }

            uint id10 = 0, id20 = 0;
            using (var ts = eng.StartTransaction())
            {
                ts.GoThrough("t", (uint id, ref RowView row) =>
                {
                    if (row.GetInt(0) == 10) id10 = id;
                    if (row.GetInt(0) == 20) id20 = id;
                    return true;
                });
                Assert.AreNotEqual(0u, id10);
                Assert.AreNotEqual(0u, id20);
                Assert.AreEqual("a", ts.Find("t", id10).Row.GetString(1));
                Assert.IsTrue(ts.Update("t", id10, "note", "aa").IsSuccess);
                Assert.IsTrue(ts.Delete("t", id20).IsSuccess);
            }

            using (var ts = eng.StartTransaction())
            {
                Assert.AreEqual("aa", ts.Find("t", "sku", 10).Row.GetString(1));
                Assert.IsFalse(ts.Find("t", "sku", 20).IsSuccess);
                Assert.AreEqual(1u, ts.Count("t", static (ref RowView _) => true).Value);
            }
        }

        [TestMethod]
        public void TwoTables_Independent_AfterCommit()
        {
            using var eng = new DbEngine();
            using (var ts = eng.StartTransaction())
            {
                ts.Create("a", [("k", DbValueType.Int, true)]);
                ts.Create("b", [("k", DbValueType.Int, true)]);
                ts.Insert("a", [("k", 1)]);
                ts.Insert("b", [("k", 2)]);
            }

            using var next = eng.StartTransaction();
            Assert.AreEqual(1, next.Find("a", "k", 1).Row.GetInt(0));
            Assert.AreEqual(2, next.Find("b", "k", 2).Row.GetInt(0));
            Assert.IsFalse(next.Find("a", "k", 2).IsSuccess);
        }

        [TestMethod]
        public void File_SaveTo_CopyIsIndependent()
        {
            var src = Configuration.GetRandomPath();
            var copy = Configuration.GetRandomPath();
            try
            {
                using (var eng = new DbEngine(src))
                {
                    using (var ts = eng.StartTransaction())
                    {
                        ts.Create("t", [("k", DbValueType.Int, true)]);
                        ts.Insert("t", [("k", 7)]);
                    }
                    eng.SaveTo(copy);
                }

                using (var a = new DbEngine(src))
                using (var ts = a.StartTransaction())
                {
                    ts.Insert("t", [("k", 8)]);
                    a.SetDestoryOnDisposed();
                }

                using (var b = new DbEngine(copy))
                using (var ts = b.StartTransaction())
                {
                    Assert.IsTrue(ts.Find("t", "k", 7).IsSuccess);
                    Assert.IsFalse(ts.Find("t", "k", 8).IsSuccess);
                    b.SetDestoryOnDisposed();
                }
            }
            finally
            {
                if (File.Exists(src)) File.Delete(src);
                if (File.Exists(copy)) File.Delete(copy);
            }
        }

        [TestMethod]
        public void DropTable_ThenCreateSameName()
        {
            using var eng = new DbEngine();
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("k", DbValueType.Int, true)]);
                ts.Insert("t", [("k", 1)]);
            }

            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Drop("t").IsSuccess);
                Assert.IsTrue(ts.Create("t", [("k", DbValueType.Int, true), ("v", DbValueType.Str8B, false)]).IsSuccess);
                ts.Insert("t", [("k", 2), ("v", "ok")]);
            }

            using var next = eng.StartTransaction();
            Assert.AreEqual("ok", next.Find("t", "k", 2).Row.GetString(1));
            Assert.IsFalse(next.Find("t", "k", 1).IsSuccess);
        }

        [TestMethod]
        public void ConcurrentReadonly_OnSharedMemory()
        {
            using var eng = new DbEngine();
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("k", DbValueType.Int, true)]);
                for (int i = 0; i < 50; i++)
                    ts.Insert("t", [("k", i)]);
            }

            var ok = 0;
            Parallel.For(0, 8, _ =>
            {
                using var ro = eng.StartTransactionReadonly();
                int n = 0;
                ro.GoThrough("t", (ref RowView row) => { n++; return true; });
                Assert.AreEqual(50, n);
                Interlocked.Increment(ref ok);
            });
            Assert.AreEqual(8, ok);
        }
    }
}
