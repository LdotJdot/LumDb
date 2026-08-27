using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    [DoNotParallelize]
    public partial class EntityJoinQueryTests
    {
        [LumEntity]
        public partial class JUser
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            public int Credit { get; set; }
            public bool Active { get; set; }
        }

        [LumEntity]
        public partial class JOrder
        {
            [Id] public uint Id { get; set; }
            public uint UserId { get; set; }
            public int Status { get; set; }
            public int Amount { get; set; }
        }

        [LumEntity]
        public partial class JLine
        {
            [Id] public uint Id { get; set; }
            public uint OrderId { get; set; }
            public int Sku { get; set; }
            public int Qty { get; set; }
        }

        public sealed class HandUser : IDbEntity
        {
            public static int Reads;
            public static void Reset() => Reads = 0;

            public uint Id { get; set; }
            public int Code { get; set; }
            public int Credit { get; set; }

            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteInt(Code);
                writer.WriteInt(Credit);
            }

            public bool TryReadFrom(IDbRow row)
            {
                Reads++;
                Code = row.GetInt(0);
                Credit = row.GetInt(1);
                return true;
            }

            public void GetId(uint id) => Id = id;
        }

        public sealed class HandOrder : IDbEntity
        {
            public static int Reads;
            public static void Reset() => Reads = 0;

            public uint Id { get; set; }
            public uint UserId { get; set; }
            public int Status { get; set; }

            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteUInt(UserId);
                writer.WriteInt(Status);
            }

            public bool TryReadFrom(IDbRow row)
            {
                Reads++;
                UserId = row.GetUInt(0);
                Status = row.GetInt(1);
                return true;
            }

            public void GetId(uint id) => Id = id;
        }

        private static void SeedLum(ITransaction ts)
        {
            ts.Create<JUser>("users");
            ts.Create<JOrder>("orders");
            ts.Create<JLine>("lines");
            for (int i = 0; i < 10; i++)
            {
                var id = ts.Insert("users", new JUser { Code = i, Credit = i * 10, Active = i % 2 == 0 }).Value;
                if (i < 6)
                {
                    var oid = ts.Insert("orders", new JOrder { UserId = id, Status = i % 2 == 0 ? 1 : 0, Amount = 40 + i }).Value;
                    ts.Insert("lines", new JLine { OrderId = oid, Sku = i, Qty = i + 1 });
                    if (i == 0)
                    {
                        var oid2 = ts.Insert("orders", new JOrder { UserId = id, Status = 1, Amount = 99 }).Value;
                        ts.Insert("lines", new JLine { OrderId = oid2, Sku = 100, Qty = 9 });
                    }
                }
            }
        }

        private static void SeedHand(ITransaction ts)
        {
            ts.Create("users", [("Code", DbValueType.Int, true), ("Credit", DbValueType.Int, false)]);
            ts.Create("orders", [("UserId", DbValueType.UInt, false), ("Status", DbValueType.Int, false)]);
            for (int i = 0; i < 10; i++)
            {
                var id = ts.Insert_Entity("users", new HandUser { Code = i, Credit = i * 10 }).Value;
                if (i < 6)
                    ts.Insert_Entity("orders", new HandOrder { UserId = id, Status = i % 2 == 0 ? 1 : 0 });
            }
        }

        [TestMethod]
        public void InnerJoin_Equi_MaterializesWindowOnly()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var pairs = ts.Query<JUser>("users")
                .Where(u => u.Active)
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .ToList();

            Assert.AreEqual(4, pairs.Count);
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 4 }, pairs.Select(p => p.Outer.Code).ToArray());
            Assert.IsTrue(pairs.All(p => p.Inner.Status == 1));
        }

        [TestMethod]
        public void InnerJoin_WhereAfterJoin_AndTake()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var n = ts.Query<JUser>("users")
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId)
                .Where((u, o) => o.Amount >= 44)
                .Count();
            Assert.AreEqual(3, n);

            var pairs = ts.Query<JUser>("users")
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId)
                .Where((u, o) => o.Amount >= 44)
                .OrderBy((u, o) => o.Amount)
                .Take(2)
                .ToList();

            Assert.AreEqual(2, pairs.Count);
            CollectionAssert.AreEqual(new[] { 44, 45 }, pairs.Select(p => p.Inner.Amount).ToArray());
        }

        [TestMethod]
        public void WhereExists_DoesNotMaterializeInner()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedHand(ts);

            HandUser.Reset();
            HandOrder.Reset();
            var users = ts.Query_Entity<HandUser>("users")
                .WhereExists<HandOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .ToList();

            Assert.AreEqual(3, users.Count);
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, users.Select(u => u.Code).ToArray());
            Assert.AreEqual(3, HandUser.Reads);
            Assert.AreEqual(0, HandOrder.Reads);
        }

        [TestMethod]
        public void WhereExists_ThenDelete_OuterOnly()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var n = ts.Query<JUser>("users")
                .WhereExists<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Delete();
            Assert.AreEqual(3, n);
            Assert.AreEqual(7, ts.Query<JUser>("users").Count());
            Assert.AreEqual(7, ts.Query<JOrder>("orders").Count());
        }

        [TestMethod]
        public void NestedLoop_InequalityResidual()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var pairs = ts.Query<JUser>("users")
                .Where(u => u.Code == 2)
                .Join<JOrder>("orders", (u, o) => u.Credit < o.Amount)
                .ToList();

            Assert.IsTrue(pairs.Count > 0);
            Assert.IsTrue(pairs.All(p => p.Outer.Code == 2 && p.Outer.Credit < p.Inner.Amount));
        }

        [TestMethod]
        public void JoinEntity_IDbEntity()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedHand(ts);

            HandUser.Reset();
            HandOrder.Reset();
            var pairs = ts.Query_Entity<HandUser>("users")
                .JoinEntity<HandOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Take(2)
                .ToList();

            Assert.AreEqual(2, pairs.Count);
            Assert.AreEqual(2, HandUser.Reads);
            Assert.AreEqual(2, HandOrder.Reads);
        }

        [TestMethod]
        public void Join_NoMatch_Empty()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var n = ts.Query<JUser>("users")
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 99)
                .Count();
            Assert.AreEqual(0, n);
            Assert.IsFalse(ts.Query<JUser>("users").WhereExists<JOrder>("orders", (u, o) => o.Status == 99).Exists());
        }

        [TestMethod]
        public void Join_OrderByInnerAmount()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var pairs = ts.Query<JUser>("users")
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .OrderByDescending((u, o) => o.Amount)
                .Take(1)
                .ToList();

            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual(99, pairs[0].Inner.Amount);
        }

        [TestMethod]
        public void ThreeTable_UserOrderLine()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var rows = ts.Query<JUser>("users")
                .Where(u => u.Active)
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Join<JLine>("lines", (u, o, l) => o.Id == l.OrderId)
                .ToList();

            Assert.AreEqual(4, rows.Count);
            Assert.IsTrue(rows.All(r => r.A.Active && r.B.Status == 1 && r.C.OrderId == r.B.Id));
        }

        [TestMethod]
        public void ThreeTable_WhereAndOrderBy()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var rows = ts.Query<JUser>("users")
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Join<JLine>("lines", (u, o, l) => o.Id == l.OrderId)
                .Where((u, o, l) => l.Qty >= 3)
                .OrderByDescending((u, o, l) => l.Qty)
                .Take(2)
                .ToList();

            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { 9, 5 }, rows.Select(r => r.C.Qty).ToArray());
        }

        [TestMethod]
        public void QueryPlan_IsLazy_CompiledUnderOneLock()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            SeedLum(ts);

            var q = ts.Query<JUser>("users").Where(u => u.Active);
            ts.Insert("users", new JUser { Code = 20, Credit = 1, Active = true });
            Assert.AreEqual(6, q.Count());

            var join = ts.Query<JUser>("users")
                .Join<JOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1);
            Assert.AreEqual(4, join.Count());
        }
    }
}
