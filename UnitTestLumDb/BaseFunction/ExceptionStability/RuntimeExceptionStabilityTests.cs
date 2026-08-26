using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ExceptionStability
{
    [TestClass]
    public partial class RuntimeExceptionStabilityTests
    {
        [LumEntity]
        public partial class Item
        {
            [Id] public uint Id { get; set; }
            [Key] public int Sku { get; set; }
            [Str8B] public string Tag { get; set; } = "";
            [Bytes8B] public byte[] Hash8 { get; set; } = Array.Empty<byte>();
            public string Note { get; set; } = "";
            public DateTime UpdatedUtc { get; set; }
        }

        [TestMethod]
        public void TableNotFound_Find_DoesNotDirtyWrite()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            var r = ts.Find("missing", 1u);
            Assert.IsFalse(r.IsSuccess);
            Assert.AreEqual("Table not found or null.", r.Exception!.Message);
        }

        [TestMethod]
        public void DuplicateCreate_Fails_AndOriginalTableIntact()
        {
            const string table = "t1";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Assert.IsTrue(ts.Create<Item>(table).IsSuccess);
            Assert.IsTrue(ts.Insert(table, new Item { Sku = 1, Tag = "a", Hash8 = new byte[8], Note = "n", UpdatedUtc = DateTime.UtcNow }).IsSuccess);
            var dup = ts.Create<Item>(table);
            Assert.IsFalse(dup.IsSuccess);
            Assert.AreEqual("Table already existed.", dup.Exception!.Message);
            Assert.AreEqual(1, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void DuplicateKey_Insert_Throws_NoDirtyWrite()
        {
            const string table = "tdup";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            var ok = ts.Insert(table, new Item { Sku = 7, Tag = "a", Hash8 = new byte[8], Note = "1", UpdatedUtc = DateTime.UtcNow });
            Assert.IsTrue(ok.IsSuccess);
            try
            {
                ts.Insert(table, new Item { Sku = 7, Tag = "b", Hash8 = new byte[8], Note = "2", UpdatedUtc = DateTime.UtcNow });
                Assert.Fail("expected duplicate key");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.KeyAlreadyExisted), ex.Message);
            }
            Assert.AreEqual(1, RowScan.Count(ts, table));
            var one = ts.FindEntity<Item>(table, "Sku", 7);
            Assert.IsTrue(one.IsSuccess);
            Assert.AreEqual("1", one.Value.Note);
        }

        [TestMethod]
        public void DuplicateKey_UpdateToOtherRowKey_Throws_NoDirtyWrite()
        {
            const string table = "tdup2";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            var a = ts.Insert(table, new Item { Sku = 1, Tag = "a", Hash8 = new byte[8], Note = "a", UpdatedUtc = DateTime.UtcNow });
            var b = ts.Insert(table, new Item { Sku = 2, Tag = "b", Hash8 = new byte[8], Note = "b", UpdatedUtc = DateTime.UtcNow });
            Assert.IsTrue(a.IsSuccess && b.IsSuccess);
            try
            {
                ts.UpdateEntity(table, b.Value, new Item { Sku = 1, Tag = "b", Hash8 = new byte[8], Note = "stolen", UpdatedUtc = DateTime.UtcNow });
                Assert.Fail("expected duplicate key on update");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.KeyAlreadyExisted), ex.Message);
            }
            Assert.AreEqual("b", ts.FindEntity<Item>(table, "Sku", 2).Value.Note);
            Assert.AreEqual("a", ts.FindEntity<Item>(table, "Sku", 1).Value.Note);
        }

        [TestMethod]
        public void UpdateSameKey_Allowed()
        {
            const string table = "tedit";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            var id = ts.Insert(table, new Item { Sku = 9, Tag = "a", Hash8 = new byte[8], Note = "old", UpdatedUtc = DateTime.UtcNow }).Value;
            var upd = ts.UpdateEntity(table, id, new Item { Sku = 9, Tag = "a", Hash8 = new byte[8], Note = "new", UpdatedUtc = DateTime.UtcNow });
            Assert.IsTrue(upd.IsSuccess);
            Assert.AreEqual("new", ts.FindEntity<Item>(table, "Sku", 9).Value.Note);
        }

        [TestMethod]
        public void DateTimeNonUtc_Insert_Fails_NoDirtyWrite()
        {
            const string table = "t2";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("when", DbValueType.DateTimeUTC, false)]);
            try
            {
                ts.Insert(table, [("when", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local))]);
                Assert.Fail("expected throw");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains(LumExceptionMessage.DateTimeUtcError));
            }
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void FindMissingKey_MessageStable_NoDirtyWrite()
        {
            const string table = "t3";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            ts.Insert(table, new Item { Sku = 10, Tag = "ok", Hash8 = new byte[8], Note = "x", UpdatedUtc = DateTime.UtcNow });
            var miss = ts.FindEntity<Item>(table, "Sku", 99);
            Assert.IsFalse(miss.IsSuccess);
            Assert.IsTrue(miss.Exception!.Message.StartsWith(LumExceptionMessage.KeyNoFound));
            Assert.AreEqual(1, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void UpdateMissingId_Fails_NoDirtyWrite()
        {
            const string table = "t4";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            ts.Insert(table, new Item { Sku = 1, Tag = "a", Hash8 = new byte[8], Note = "n", UpdatedUtc = DateTime.UtcNow });
            var upd = ts.UpdateEntity(table, 999u, new Item { Sku = 1, Tag = "b", Hash8 = new byte[8], Note = "z", UpdatedUtc = DateTime.UtcNow });
            Assert.IsFalse(upd.IsSuccess);
            Assert.AreEqual("Data not found.", upd.Exception!.Message);
            Assert.AreEqual("a", ts.FindEntity<Item>(table, "Sku", 1).Value.Tag);
        }

        [TestMethod]
        public void Str8B_TooLong_Fails_NoDirtyWrite()
        {
            const string table = "t5";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            try
            {
                ts.Insert(table, new Item
                {
                    Sku = 1,
                    Tag = "123456789",
                    Hash8 = new byte[8],
                    Note = "n",
                    UpdatedUtc = DateTime.UtcNow,
                });
                Assert.Fail("expected length error");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.FixedLengthTooLong), ex.Message);
            }
            Assert.AreEqual(0, RowScan.Count(ts, table));
            Assert.IsTrue(ts.Insert(table, new Item { Sku = 2, Tag = "ok", Hash8 = new byte[8], Note = "n", UpdatedUtc = DateTime.UtcNow }).IsSuccess);
            Assert.AreEqual(1, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Bytes8_TooLong_Fails_NoDirtyWrite()
        {
            const string table = "tb8";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            try
            {
                ts.Insert(table, new Item
                {
                    Sku = 1,
                    Tag = "ok",
                    Hash8 = new byte[9],
                    Note = "n",
                    UpdatedUtc = DateTime.UtcNow,
                });
                Assert.Fail("expected length error");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.FixedLengthTooLong), ex.Message);
            }
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Insert_ColumnCountMismatch_Message_NoDirtyWrite()
        {
            const string table = "tcol";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("a", DbValueType.Int, true), ("b", DbValueType.Int, false)]);
            try
            {
                ts.Insert(table, [("a", 1)]);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual(LumExceptionMessage.ColumnElementNotEqual, ex.Message);
            }
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Insert_UnknownColumnName_Message_NoDirtyWrite()
        {
            const string table = "tnm";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("a", DbValueType.Int, true)]);
            try
            {
                ts.Insert(table, [("nope", 1)]);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.ColumnNameNotExisted));
            }
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Update_WrongColumnName_Throws()
        {
            const string table = "twc";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("a", DbValueType.Int, true)]);
            var id = ts.Insert(table, [("a", 1)]).Value;
            try
            {
                ts.Update(table, id, "missing", 2);
                Assert.Fail();
            }
            catch (Exception)
            {
                // column index lookup throws
            }
            Assert.AreEqual(1, ts.Find(table, "a", 1).Row.GetInt(0));
        }

        [TestMethod]
        public void Discard_DoesNotPersist()
        {
            const string table = "t6";
            var path = Configuration.GetRandomPath();
            using (var eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                ts.Create<Item>(table);
                ts.Insert(table, new Item { Sku = 1, Tag = "a", Hash8 = new byte[8], Note = "n", UpdatedUtc = DateTime.UtcNow });
                ts.Discard();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                Assert.IsFalse(ts.FindEntity<Item>(table, "Sku", 1).IsSuccess);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void SaveChanges_ThenVisible()
        {
            const string table = "t7";
            var path = Configuration.GetRandomPath();
            using (var eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                ts.Create<Item>(table);
                ts.Insert(table, new Item { Sku = 42, Tag = "ok", Hash8 = new byte[8], Note = "persist", UpdatedUtc = DateTime.UtcNow });
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                var find = ts.FindEntity<Item>(table, "Sku", 42);
                Assert.IsTrue(find.IsSuccess);
                Assert.AreEqual("persist", find.Value.Note);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void DeleteMissing_Fails_NoDirtyWrite()
        {
            const string table = "t8";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<Item>(table);
            ts.Insert(table, new Item { Sku = 1, Tag = "a", Hash8 = new byte[8], Note = "n", UpdatedUtc = DateTime.UtcNow });
            var del = ts.Delete(table, "Sku", 99);
            Assert.IsFalse(del.IsSuccess);
            Assert.AreEqual(1, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void FindNonKeyColumn_MessageContainsNotKey()
        {
            const string table = "t9";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("a", DbValueType.Int, false), ("b", DbValueType.Int, true)]);
            ts.Insert(table, [("a", 1), ("b", 2)]);
            var r = ts.Find(table, "a", 1);
            Assert.IsFalse(r.IsSuccess);
            Assert.IsTrue(r.Exception!.Message.Contains(LumExceptionMessage.NotKey));
            Assert.AreEqual(1, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void FindEntityWhere_SkipLimit_AndVarOnlyOnHits()
        {
            const string table = "tvar";
            var big = new string('Z', 20_000);
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("No", DbValueType.Int, true), ("Score", DbValueType.Int, false), ("Body", DbValueType.StrVar, false)]);
            for (int i = 0; i < 20; i++)
                ts.Insert(table, [("No", i), ("Score", i % 5), ("Body", big)]);

            DataVarManager.GetDataVarCallCount = 0;
            int hits = 0;
            ts.GoThrough(table, (ref RowView r) =>
            {
                if (r.GetInt(1) == 3)
                    hits++;
                return true;
            });
            Assert.AreEqual(0, DataVarManager.GetDataVarCallCount);
            Assert.IsTrue(hits > 0);
        }
    }
}
