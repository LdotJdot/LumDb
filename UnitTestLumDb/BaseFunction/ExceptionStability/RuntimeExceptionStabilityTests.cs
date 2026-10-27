using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;
using UnitTestLumDb.DefectHunt;

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

        private static Item Row(int sku, string tag, string note, int hashBytes = 8) => new()
        {
            Sku = sku,
            Tag = tag,
            Hash8 = new byte[hashBytes],
            Note = note,
            UpdatedUtc = DateTime.UtcNow,
        };

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
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create<Item>(table).IsSuccess);
                Assert.IsTrue(seed.Insert(table, Row(7, "a", "1")).IsSuccess);
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            var duplicate = DhSupport.Capture(() => ts.Insert(table, Row(7, "b", "2")));
            Assert.IsNotNull(duplicate, "重复键必须抛出");
            Assert.IsTrue(duplicate!.Message.StartsWith(LumExceptionMessage.KeyAlreadyExisted), duplicate.Message);
            Assert.IsTrue(((LumTransaction)ts).IsFaulted, "失败语句之后事务必须作废");

            // 作废后同一事务连读语句都不再允许（工作集已被丢弃）。
            var afterFailure = DhSupport.Capture(() => ts.FindEntity<Item>(table, "Sku", 7));
            Assert.IsTrue(DhSupport.IsAborted(afterFailure),
                $"作废事务的后续语句必须被拒绝，实际：{DhSupport.Describe(afterFailure)}");
            Assert.IsTrue(DhSupport.IsAborted(DhSupport.Capture(ts.SaveChanges)), "作废事务不得提交");

            // 显式回滚：回到已提交镜像，失败语句不留下任何脏写。
            ts.Discard();
            Assert.IsFalse(((LumTransaction)ts).IsFaulted, "Discard 之后事务应恢复可用");
            Assert.AreEqual(1, RowScan.Count(ts, table), "失败语句不得改动已提交镜像");
            var one = ts.FindEntity<Item>(table, "Sku", 7);
            Assert.IsTrue(one.IsSuccess);
            Assert.AreEqual("1", one.Value.Note);
        }

        [TestMethod]
        public void DuplicateKey_UpdateToOtherRowKey_Throws_NoDirtyWrite()
        {
            const string table = "tdup2";
            using var eng = Configuration.GetDbEngineForTest();
            uint idB;
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create<Item>(table).IsSuccess);
                Assert.IsTrue(seed.Insert(table, Row(1, "a", "a")).IsSuccess);
                var b = seed.Insert(table, Row(2, "b", "b"));
                Assert.IsTrue(b.IsSuccess);
                idB = b.Value;
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            var dup = DhSupport.Capture(() => ts.UpdateEntity(table, idB, Row(1, "b", "stolen")));
            Assert.IsNotNull(dup, "把行更新成另一个已存在的键必须抛出");
            Assert.IsTrue(dup!.Message.StartsWith(LumExceptionMessage.KeyAlreadyExisted), dup.Message);
            Assert.IsTrue(((LumTransaction)ts).IsFaulted, "失败的更新必须作废整个事务");

            ts.Discard();
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
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create<Item>(table).IsSuccess);
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            var tooLong = DhSupport.Capture(() => ts.Insert(table, Row(1, "123456789", "n")));
            Assert.IsNotNull(tooLong, "超长 Str8B 必须抛出");
            Assert.IsTrue(tooLong!.Message.StartsWith(LumExceptionMessage.FixedLengthTooLong), tooLong.Message);
            Assert.IsTrue(DhSupport.IsAborted(DhSupport.Capture(() => ts.Insert(table, Row(2, "ok", "n")))),
                "作废后继续插入必须在到达存储层前被拒绝");

            ts.Discard();
            Assert.AreEqual(0, RowScan.Count(ts, table));
            Assert.IsTrue(ts.Insert(table, Row(2, "ok", "n")).IsSuccess);
            Assert.AreEqual(1, RowScan.Count(ts, table));
            ts.SaveChanges();
        }

        [TestMethod]
        public void Bytes8_TooLong_Fails_NoDirtyWrite()
        {
            const string table = "tb8";
            using var eng = Configuration.GetDbEngineForTest();
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create<Item>(table).IsSuccess);
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            var tooLong = DhSupport.Capture(() => ts.Insert(table, Row(1, "ok", "n", hashBytes: 9)));
            Assert.IsNotNull(tooLong, "超长 Bytes8B 必须抛出");
            Assert.IsTrue(tooLong!.Message.StartsWith(LumExceptionMessage.FixedLengthTooLong), tooLong.Message);
            Assert.IsTrue(((LumTransaction)ts).IsFaulted, "失败的插入必须作废整个事务");

            ts.Discard();
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Insert_ColumnCountMismatch_Message_NoDirtyWrite()
        {
            const string table = "tcol";
            using var eng = Configuration.GetDbEngineForTest();
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create(table, [("a", DbValueType.Int, true), ("b", DbValueType.Int, false)]).IsSuccess);
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            var bad = DhSupport.Capture(() => ts.Insert(table, [("a", 1)]));
            Assert.IsNotNull(bad, "列数不匹配必须抛出");
            Assert.AreEqual(LumExceptionMessage.ColumnElementNotEqual, bad!.Message);
            Assert.IsTrue(((LumTransaction)ts).IsFaulted);

            ts.Discard();
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Insert_UnknownColumnName_Message_NoDirtyWrite()
        {
            const string table = "tnm";
            using var eng = Configuration.GetDbEngineForTest();
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create(table, [("a", DbValueType.Int, true)]).IsSuccess);
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            var bad = DhSupport.Capture(() => ts.Insert(table, [("nope", 1)]));
            Assert.IsNotNull(bad, "未知列名必须抛出");
            Assert.IsTrue(bad!.Message.StartsWith(LumExceptionMessage.ColumnNameNotExisted), bad.Message);
            Assert.IsTrue(((LumTransaction)ts).IsFaulted);

            ts.Discard();
            Assert.AreEqual(0, RowScan.Count(ts, table));
        }

        [TestMethod]
        public void Update_WrongColumnName_Throws()
        {
            const string table = "twc";
            using var eng = Configuration.GetDbEngineForTest();
            uint id;
            using (var seed = eng.StartTransaction())
            {
                Assert.IsTrue(seed.Create(table, [("a", DbValueType.Int, true)]).IsSuccess);
                var inserted = seed.Insert(table, [("a", 1)]);
                Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
                id = inserted.Value;
                seed.SaveChanges();
            }

            using var ts = eng.StartTransaction();
            // column index lookup throws
            var wrong = DhSupport.Capture(() => ts.Update(table, id, "missing", 2));
            Assert.IsNotNull(wrong, "未知列名的更新必须抛出");
            Assert.IsTrue(((LumTransaction)ts).IsFaulted, "失败的更新必须作废整个事务");

            ts.Discard();
            var row = ts.Find(table, "a", 1);
            Assert.IsTrue(row.IsSuccess, row.Exception?.Message);
            Assert.AreEqual(1, row.Row.GetInt(0));
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
