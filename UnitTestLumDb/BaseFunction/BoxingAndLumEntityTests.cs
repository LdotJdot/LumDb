using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    /// <summary>
    /// Covers typed WriteTo/TryReadFrom (no object[] Boxing) and [LumEntity] partial (no reflection).
    /// Does not assert page-link / LastDataPageId behavior.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BoxingAndLumEntityTests
    {
        [TestMethod]
        public void IDbEntity_HasNoObjectArrayBoxingApi()
        {
            var methods = typeof(IDbEntity).GetMethods();
            Assert.IsFalse(methods.Any(m => m.Name == "Boxing"), "object[] Boxing must stay removed");
            Assert.IsFalse(methods.Any(m =>
                    m.Name == "Unboxing"
                    && m.GetParameters() is { Length: 1 } p
                    && p[0].ParameterType == typeof(object[])),
                "Unboxing(object[]) must stay removed");
            Assert.IsNotNull(typeof(IDbEntity).GetMethod(nameof(IDbEntity.WriteTo)));
            Assert.IsNotNull(typeof(IDbEntity).GetMethod(nameof(IDbEntity.TryReadFrom)));
        }

        [TestMethod]
        public void IDbEntity_InsertFindUpdate_TypedWriteRead()
        {
            const string table = "typedRows";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(table, [("uid", DbValueType.Int, true), ("name", DbValueType.Str32B, false)]);

            var id = ts.Insert_Entity(table, new TypedRow { Uid = 7, Name = "alpha" });
            Assert.IsTrue(id.IsSuccess);
            Assert.AreEqual(1u, id.Value);

            var byKey = ts.Find_Entity<TypedRow>(table, "uid", 7);
            Assert.IsTrue(byKey.IsSuccess);
            Assert.AreEqual(7, byKey.Value.Uid);
            Assert.AreEqual("alpha", byKey.Value.Name);
            Assert.AreEqual(1u, byKey.Value.Id);

            var byId = ts.Find_Entity<TypedRow>(table, 1u);
            Assert.IsTrue(byId.IsSuccess);
            Assert.AreEqual("alpha", byId.Value.Name);

            Assert.IsTrue(ts.Update_Entity(table, 1u, new TypedRow { Uid = 7, Name = "beta" }).IsSuccess);
            Assert.AreEqual("beta", ts.Find_Entity<TypedRow>(table, "uid", 7).Value.Name);

            var linq = ts.Query_Entity<TypedRow>(table).Where(x => x.Name == "beta").Take(1).ToValues();
            Assert.AreEqual(1, linq.Values.Count);
            Assert.AreEqual(7, linq.Values[0].Uid);
        }

        [TestMethod]
        public void LumEntity_Partial_CreateInsertFindUpdate_WithoutReflection()
        {
            const string table = "lumRows";
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();

            Assert.IsTrue(ts.Create<LumRow>(table).IsSuccess);

            var schema = LumRow.DbSchema;
            Assert.AreEqual(2, schema.Length);
            Assert.AreEqual("Code", schema[0].columnName);
            Assert.AreEqual(DbValueType.Int, schema[0].type);
            Assert.IsTrue(schema[0].isKey);
            Assert.AreEqual("Title", schema[1].columnName);

            var inserted = ts.Insert(table, new LumRow { Code = 42, Title = "hello" });
            Assert.IsTrue(inserted.IsSuccess);
            Assert.AreEqual(1u, inserted.Value);

            var byId = ts.FindByIdEntity<LumRow>(table, 1u);
            Assert.IsTrue(byId.IsSuccess);
            Assert.AreEqual(1u, byId.Value.Id);
            Assert.AreEqual(42, byId.Value.Code);
            Assert.AreEqual("hello", byId.Value.Title);

            var byKey = ts.FindEntity<LumRow>(table, "Code", 42);
            Assert.IsTrue(byKey.IsSuccess);
            Assert.AreEqual("hello", byKey.Value.Title);

            Assert.IsTrue(ts.UpdateEntity(table, 1u, new LumRow { Code = 42, Title = "world" }).IsSuccess);
            Assert.AreEqual("world", ts.FindByIdEntity<LumRow>(table, 1u).Value.Title);

            ts.Insert(table, new LumRow { Code = 43, Title = "skip-me" });
            ts.Insert(table, new LumRow { Code = 44, Title = "keep" });

            var page = ts.Find<LumRow>(table, (ref RowView row) => LumRow.GetCode(ref row) >= 43, skip: 1, limit: 1);
            Assert.AreEqual(1, page.Values.Count);
            Assert.AreEqual(44, page.Values[0].Code);
            Assert.AreEqual("keep", page.Values[0].Title);

            var count = ts.Count(table, (ref RowView row) => row.GetInt(0) >= 42);
            Assert.AreEqual(3u, count.Value);
        }

        [TestMethod]
        public void LumEntity_ImplementsILumEntity_NotIDbEntity()
        {
            Assert.IsTrue(typeof(ILumEntity<LumRow>).IsAssignableFrom(typeof(LumRow)));
            Assert.IsFalse(typeof(IDbEntity).IsAssignableFrom(typeof(LumRow)));
            Assert.IsNull(typeof(LumRow).GetMethod("Boxing"));
        }

        [TestMethod]
        public void Find_Entity_LinqTake_DoesNotMaterializeWholeTable()
        {
            const string table = "linqTake";
            CountedRow.Reset();
            using var eng = Configuration.GetDbEngineForTest();
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("n", DbValueType.Int, true)]);
                for (int i = 0; i < 2000; i++)
                    ts.Insert_Entity(table, new CountedRow { N = i });
            }

            CountedRow.Reset();
            using (var ts = eng.StartTransactionReadonly())
            {
                var page = ts.Query_Entity<CountedRow>(table).Take(7).ToValues();
                Assert.AreEqual(7, page.Values.Count);
                Assert.AreEqual(0, page.Values[0].N);
                Assert.AreEqual(6, page.Values[6].N);
            }

            Assert.AreEqual(7, CountedRow.Reads);
        }

        [TestMethod]
        public void Find_Entity_LinqWhereSkipTake_StopsWhenWindowFills()
        {
            const string table = "linqWindow";
            using var eng = Configuration.GetDbEngineForTest();
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("n", DbValueType.Int, true)]);
                for (int i = 0; i < 500; i++)
                    ts.Insert_Entity(table, new CountedRow { N = i });
            }

            CountedRow.Reset();
            using (var ts = eng.StartTransactionReadonly())
            {
                var page = ts.Query_Entity<CountedRow>(table).Where(x => x.N % 2 == 0).Skip(10).Take(5).ToValues();
                CollectionAssert.AreEqual(new[] { 20, 22, 24, 26, 28 }, page.Values.Select(x => x.N).ToArray());
            }

            Assert.AreEqual(5, CountedRow.Reads);
        }

        [TestMethod]
        public void Find_Entity_LinqBackwardTake_DoesNotMaterializeWholeTable()
        {
            const string table = "linqBack";
            using var eng = Configuration.GetDbEngineForTest();
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("n", DbValueType.Int, true)]);
                for (int i = 0; i < 200; i++)
                    ts.Insert_Entity(table, new CountedRow { N = i });
            }

            CountedRow.Reset();
            using (var ts = eng.StartTransactionReadonly())
            {
                var page = ts.Query_Entity<CountedRow>(table).Reverse().Take(3).ToValues();
                CollectionAssert.AreEqual(new[] { 199, 198, 197 }, page.Values.Select(x => x.N).ToArray());
            }

            Assert.AreEqual(3, CountedRow.Reads);
        }
    }

    public class CountedRow : IDbEntity
    {
        public static int Reads;

        public static void Reset() => Reads = 0;

        public int N { get; set; }

        public void WriteTo(ref RowWriter writer) => writer.WriteInt(N);

        public bool TryReadFrom(IDbRow row)
        {
            Reads++;
            N = row.GetInt(0);
            return true;
        }
    }

    public class TypedRow : IDbEntity
    {
        public uint Id { get; set; }
        public int Uid { get; set; }
        public string Name { get; set; } = "";

        public void WriteTo(ref RowWriter writer)
        {
            writer.WriteInt(Uid);
            writer.WriteString(Name);
        }

        public bool TryReadFrom(IDbRow row)
        {
            Uid = row.GetInt(0);
            Name = row.GetString(1);
            return true;
        }

        public void GetId(uint id) => Id = id;
    }

    [LumEntity]
    public partial class LumRow
    {
        [Id] public uint Id { get; set; }
        [Key] public int Code { get; set; }
        [Str32B] public string Title { get; set; } = "";
    }
}
