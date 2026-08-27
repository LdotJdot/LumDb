using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    [DoNotParallelize]
    public partial class EntityExpressionQueryTests
    {
        [LumEntity]
        public partial class Item
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            public int Score { get; set; }
            [Str32B] public string Name { get; set; } = "";
        }

        public sealed class HandItem : IDbEntity
        {
            public static int Reads;
            public static void Reset() => Reads = 0;

            public uint Id { get; set; }
            public int Code { get; set; }
            public int Score { get; set; }
            public string Name { get; set; } = "";

            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteInt(Code);
                writer.WriteInt(Score);
                writer.WriteString(Name);
            }

            public bool TryReadFrom(IDbRow row)
            {
                Reads++;
                Code = row.GetInt(0);
                Score = row.GetInt(1);
                Name = row.GetString(2);
                return true;
            }

            public void GetId(uint id) => Id = id;
        }

        private static void Seed(ITransaction ts, string table, bool lum)
        {
            if (lum)
                ts.Create<Item>(table);
            else
                ts.Create(table, [("Code", DbValueType.Int, true), ("Score", DbValueType.Int, false), ("Name", DbValueType.Str32B, false)]);

            for (int i = 0; i < 50; i++)
            {
                if (lum)
                    ts.Insert(table, new Item { Code = i, Score = i * 10, Name = "n" + i });
                else
                    ts.Insert_Entity(table, new HandItem { Code = i, Score = i * 10, Name = "n" + i });
            }
        }

        [TestMethod]
        public void LumEntity_WhereSkipTake_OnRowView()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            var page = ts.Query<Item>("t").Where(x => x.Code % 2 == 0).Skip(5).Take(3).ToList();
            CollectionAssert.AreEqual(new[] { 10, 12, 14 }, page.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void IDbEntity_WhereSkipTake_DoesNotUnboxSkipped()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: false);

            HandItem.Reset();
            var page = ts.Query_Entity<HandItem>("t").Where(x => x.Code % 2 == 0).Skip(5).Take(3).ToList();
            CollectionAssert.AreEqual(new[] { 10, 12, 14 }, page.Select(x => x.Code).ToArray());
            Assert.AreEqual(3, HandItem.Reads);
        }

        [TestMethod]
        public void Reverse_Take_NewestFirst()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: false);

            HandItem.Reset();
            var page = ts.Query_Entity<HandItem>("t").Reverse().Take(3).ToList();
            CollectionAssert.AreEqual(new[] { 49, 48, 47 }, page.Select(x => x.Code).ToArray());
            Assert.AreEqual(3, HandItem.Reads);
        }

        [TestMethod]
        public void OrderByDescending_Take_MaterializesWindowOnly()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: false);

            HandItem.Reset();
            var page = ts.Query_Entity<HandItem>("t").OrderByDescending(x => x.Score).Take(4).ToList();
            CollectionAssert.AreEqual(new[] { 49, 48, 47, 46 }, page.Select(x => x.Code).ToArray());
            Assert.AreEqual(4, HandItem.Reads);
        }

        [TestMethod]
        public void AndOrNot_StringEndsWith_CapturedConstant()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            var suffix = "2";
            var hit = ts.Query<Item>("t")
                .Where(x => x.Name.EndsWith(suffix) && x.Code >= 10 && !(x.Score < 100))
                .ToList();
            Assert.IsTrue(hit.All(x => x.Name.EndsWith("2") && x.Code >= 10 && x.Score >= 100));
            Assert.IsTrue(hit.Count > 0);
        }

        [TestMethod]
        public void Count_Exists_FirstOrDefault_FindOverload()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            Assert.AreEqual(25, ts.Query<Item>("t").Where(x => x.Code % 2 == 0).Count());
            Assert.IsTrue(ts.Query<Item>("t").Where(x => x.Code == 7).Exists());
            Assert.IsFalse(ts.Query<Item>("t").Where(x => x.Code == 999).Exists());
            Assert.AreEqual(3, ts.Query<Item>("t").Where(x => x.Code > 2).FirstOrDefault()!.Code);

            var found = ts.Find<Item>("t", x => x.Code == 8);
            Assert.AreEqual(1, found.Values.Count);
            Assert.AreEqual(8, found.Values[0].Code);

            var page = ts.Find<Item>("t", x => x.Code >= 40, skip: 2, limit: 3);
            CollectionAssert.AreEqual(new[] { 42, 43, 44 }, page.Values.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void OrderBy_EngineId()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            var desc = ts.Query<Item>("t").OrderByDescending(x => x.Id).Take(2).ToList();
            Assert.AreEqual(2, desc.Count);
            Assert.IsTrue(desc[0].Id > desc[1].Id);
        }

        [TestMethod]
        public void UnsupportedExpression_Throws()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            try
            {
                ts.Query<Item>("t").Where(x => x.Name.Split('n').Length > 0).ToList();
                Assert.Fail("expected LumException");
            }
            catch (LumException)
            {
            }
        }

        [TestMethod]
        public void Update_Entity_ExpressionPredicate()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: false);

            Assert.IsTrue(ts.Update_Entity("t", x => x.Code == 5, new HandItem { Code = 5, Score = 1, Name = "upd" }).IsSuccess);
            var got = ts.Find_Entity<HandItem>("t", "Code", 5);
            Assert.AreEqual("upd", got.Value.Name);
            Assert.AreEqual(1, got.Value.Score);
        }

        [TestMethod]
        public void MultipleWhere_AndCombined()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            var list = ts.Query<Item>("t").Where(x => x.Code >= 10).Where(x => x.Code < 13).ToList();
            CollectionAssert.AreEqual(new[] { 10, 11, 12 }, list.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void Delete_Entity_Where_DoesNotUnbox()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: false);

            HandItem.Reset();
            var n = ts.Delete_Entity<HandItem>("t", x => x.Code % 2 == 0);
            Assert.AreEqual(25u, n.Value);
            Assert.AreEqual(0, HandItem.Reads);

            var left = ts.Query_Entity<HandItem>("t").ToList();
            Assert.AreEqual(25, left.Count);
            Assert.IsTrue(left.All(x => x.Code % 2 != 0));
        }

        [TestMethod]
        public void Delete_LumEntity_Where()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            var n = ts.Delete<Item>("t", x => x.Code < 10);
            Assert.AreEqual(10u, n.Value);
            Assert.AreEqual(40, ts.Query<Item>("t").Count());
            Assert.AreEqual(10, ts.Query<Item>("t").Where(x => x.Code >= 10).First().Code);
        }

        [TestMethod]
        public void Query_Take_Delete_OccupancyWindow()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: false);

            var n = ts.Query_Entity<HandItem>("t").Where(x => x.Code >= 40).Take(3).Delete();
            Assert.AreEqual(3, n);
            var left = ts.Query_Entity<HandItem>("t").Where(x => x.Code >= 40).ToList().Select(x => x.Code).ToArray();
            CollectionAssert.AreEqual(new[] { 43, 44, 45, 46, 47, 48, 49 }, left);
        }

        [TestMethod]
        public void Query_OrderByDescending_Take_Delete()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts, "t", lum: true);

            var n = ts.Query<Item>("t").OrderByDescending(x => x.Score).Take(2).Delete();
            Assert.AreEqual(2, n);
            Assert.IsFalse(ts.Query<Item>("t").Where(x => x.Code == 49).Exists());
            Assert.IsFalse(ts.Query<Item>("t").Where(x => x.Code == 48).Exists());
            Assert.IsTrue(ts.Query<Item>("t").Where(x => x.Code == 47).Exists());
        }
    }
}
