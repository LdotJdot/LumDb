using LumDbEngine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    /// <summary>
    /// Drill Join / WhereExists / mixed types / random multi-step oracles.
    /// If this class fails, treat it as a finding — do not patch the engine from here first.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public partial class EntityQueryStressOracleTests
    {
        [LumEntity]
        public partial class SUser
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            public int Score { get; set; }
            public bool Active { get; set; }
            public double Val { get; set; }
            [Str32B] public string Tag { get; set; } = "";
            public DateTime When { get; set; }
        }

        [LumEntity]
        public partial class SOrder
        {
            [Id] public uint Id { get; set; }
            public uint UserId { get; set; }
            public int Status { get; set; }
            public int Amount { get; set; }
            [Str16B] public string Label { get; set; } = "";
        }

        [LumEntity]
        public partial class SLine
        {
            [Id] public uint Id { get; set; }
            public uint OrderId { get; set; }
            public int Qty { get; set; }
            public byte Flag { get; set; }
        }

        public sealed class CountUser : IDbEntity
        {
            public static int Reads;
            public static void Reset() => Reads = 0;
            public uint Id { get; set; }
            public int Code { get; set; }
            public void WriteTo(ref RowWriter writer) => writer.WriteInt(Code);
            public bool TryReadFrom(IDbRow row)
            {
                Reads++;
                Code = row.GetInt(0);
                return true;
            }
            public void GetId(uint id) => Id = id;
        }

        public sealed class CountOrder : IDbEntity
        {
            public static int Reads;
            public static void Reset() => Reads = 0;
            public uint Id { get; set; }
            public uint UserId { get; set; }
            public int Amount { get; set; }
            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteUInt(UserId);
                writer.WriteInt(Amount);
            }
            public bool TryReadFrom(IDbRow row)
            {
                Reads++;
                UserId = row.GetUInt(0);
                Amount = row.GetInt(1);
                return true;
            }
            public void GetId(uint id) => Id = id;
        }

        public delegate bool MixPred(int score, string tag, bool active);

        private sealed class Gold
        {
            public readonly Dictionary<uint, SUser> Users = new();
            public readonly Dictionary<uint, SOrder> Orders = new();
            public readonly Dictionary<uint, SLine> Lines = new();
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(7)]
        [DataRow(42)]
        [DataRow(99)]
        [DataRow(20260827)]
        [DataRow(123456)]
        [Timeout(180000)]
        public void RandomMultiStep_Oracle(int seed)
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 20000;
            using var ts = eng.StartTransaction();
            ts.Create<SUser>("users");
            ts.Create<SOrder>("orders");
            ts.Create<SLine>("lines");

            var rng = new Random(seed);
            var gold = new Gold();
            var nextCode = 1;
            var t0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            void InsUser()
            {
                var code = nextCode++;
                var u = new SUser
                {
                    Code = code,
                    Score = rng.Next(-50, 200),
                    Active = rng.Next(2) == 0,
                    Val = rng.Next(0, 100) + rng.NextDouble(),
                    Tag = "t" + (code % 17),
                    When = t0.AddMinutes(code),
                };
                var id = ts.Insert("users", u).Value;
                u.Id = id;
                gold.Users[id] = u;
            }

            void InsOrder()
            {
                if (gold.Users.Count == 0)
                {
                    InsUser();
                    return;
                }

                var u = gold.Users.Values.ElementAt(rng.Next(gold.Users.Count));
                var o = new SOrder
                {
                    UserId = u.Id,
                    Status = rng.Next(3),
                    Amount = rng.Next(1, 80),
                    Label = u.Tag.ToUpperInvariant(),
                };
                var id = ts.Insert("orders", o).Value;
                o.Id = id;
                gold.Orders[id] = o;
            }

            void InsLine()
            {
                if (gold.Orders.Count == 0)
                {
                    InsOrder();
                    return;
                }

                var o = gold.Orders.Values.ElementAt(rng.Next(gold.Orders.Count));
                var l = new SLine { OrderId = o.Id, Qty = rng.Next(1, 12), Flag = (byte)rng.Next(4) };
                var id = ts.Insert("lines", l).Value;
                l.Id = id;
                gold.Lines[id] = l;
            }

            for (int i = 0; i < 80; i++)
                InsUser();
            for (int i = 0; i < 180; i++)
                InsOrder();
            for (int i = 0; i < 220; i++)
                InsLine();

            for (int step = 0; step < 400; step++)
            {
                var op = rng.Next(100);
                if (op < 18)
                    InsUser();
                else if (op < 40)
                    InsOrder();
                else if (op < 55)
                    InsLine();
                else if (op < 70 && gold.Users.Count > 0)
                {
                    var u = gold.Users.Values.ElementAt(rng.Next(gold.Users.Count));
                    u.Score = rng.Next(-50, 200);
                    u.Active = rng.Next(2) == 0;
                    u.Val = rng.Next(0, 80) + 0.25;
                    Assert.IsTrue(ts.UpdateEntity("users", "Code", u.Code, u).IsSuccess);
                }
                else if (op < 78 && gold.Users.Count > 8)
                {
                    var u = gold.Users.Values.ElementAt(rng.Next(gold.Users.Count));
                    Assert.AreEqual(1, ts.Query<SUser>("users").Where(x => x.Code == u.Code).Delete());
                    gold.Users.Remove(u.Id);
                }
                else if (op < 84 && gold.Orders.Count > 10)
                {
                    var o = gold.Orders.Values.ElementAt(rng.Next(gold.Orders.Count));
                    Assert.AreEqual(1, ts.Query<SOrder>("orders").Where(x => x.Id == o.Id).Delete());
                    gold.Orders.Remove(o.Id);
                    foreach (var lid in gold.Lines.Where(kv => kv.Value.OrderId == o.Id).Select(kv => kv.Key).ToList())
                    {
                        Assert.AreEqual(1, ts.Query<SLine>("lines").Where(x => x.Id == lid).Delete());
                        gold.Lines.Remove(lid);
                    }
                }
                else if (op < 88 && gold.Lines.Count > 10)
                {
                    var l = gold.Lines.Values.ElementAt(rng.Next(gold.Lines.Count));
                    Assert.AreEqual(1, ts.Query<SLine>("lines").Where(x => x.Id == l.Id).Delete());
                    gold.Lines.Remove(l.Id);
                }

                if (step % 19 == 0)
                    AssertGold(ts, gold, t0, seed, step, deep: false);
            }

            AssertGold(ts, gold, t0, seed, -1, deep: true);
        }

        private static void AssertGold(ITransaction ts, Gold gold, DateTime t0, int seed, int step, bool deep)
        {
            var ctx = $"seed={seed} step={step} u={gold.Users.Count} o={gold.Orders.Count} l={gold.Lines.Count}";

            Assert.AreEqual(gold.Users.Count, ts.Query<SUser>("users").Count(), ctx + " users");
            Assert.AreEqual(gold.Orders.Count, ts.Query<SOrder>("orders").Count(), ctx + " orders");
            Assert.AreEqual(gold.Lines.Count, ts.Query<SLine>("lines").Count(), ctx + " lines");

            Assert.AreEqual(
                gold.Users.Values.Count(u => u.Active),
                ts.Query<SUser>("users").Where(u => u.Active).Count(),
                ctx + " active");

            Assert.AreEqual(
                gold.Users.Values.Count(u => u.Score >= 40 && u.Val < 70),
                ts.Query<SUser>("users").Where(u => u.Score >= 40 && u.Val < 70).Count(),
                ctx + " score/val");

            var cutoff = t0.AddMinutes(20);
            Assert.AreEqual(
                gold.Users.Values.Count(u => u.When >= cutoff && u.Tag.StartsWith("t")),
                ts.Query<SUser>("users").Where(u => u.When >= cutoff && u.Tag.StartsWith("t")).Count(),
                ctx + " when");

            MixPred mix = static (s, tag, a) => a && s >= 10 && tag.Length > 0;
            Assert.AreEqual(
                gold.Users.Values.Count(u => mix(u.Score, u.Tag, u.Active)),
                ts.Query<SUser>("users").Where(u => mix(u.Score, u.Tag, u.Active)).Count(),
                ctx + " mix pred");

            var joinGold = (
                from u in gold.Users.Values
                join o in gold.Orders.Values on u.Id equals o.UserId
                where u.Active && o.Status == 1
                select (u.Code, o.Amount)).OrderBy(p => p.Code).ThenBy(p => p.Amount).ToArray();

            Assert.AreEqual(
                joinGold.Length,
                ts.Query<SUser>("users")
                    .Where(u => u.Active)
                    .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                    .Count(),
                ctx + " join count");

            var existsGold = gold.Users.Values.Count(u =>
                gold.Orders.Values.Any(o => o.UserId == u.Id && o.Status == 1));
            Assert.AreEqual(
                existsGold,
                ts.Query<SUser>("users")
                    .WhereExists<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                    .Count(),
                ctx + " exists");

            var existsTag = gold.Users.Values.Count(u =>
                gold.Orders.Values.Any(o => o.UserId == u.Id && o.Label == u.Tag.ToUpperInvariant() && o.Amount >= 10));
            Assert.AreEqual(
                existsTag,
                ts.Query<SUser>("users")
                    .WhereExists<SOrder>("orders", (u, o) =>
                        u.Id == o.UserId && u.Tag.ToUpper() == o.Label && o.Amount >= 10)
                    .Count(),
                ctx + " exists upper");

            var triGold = (
                from u in gold.Users.Values
                join o in gold.Orders.Values on u.Id equals o.UserId
                join l in gold.Lines.Values on o.Id equals l.OrderId
                where u.Active && o.Status == 1 && l.Qty >= 3 && l.Flag != 3
                select (u.Code, o.Amount, l.Qty, l.Flag)).OrderBy(t => t.Code).ThenBy(t => t.Amount).ThenBy(t => t.Qty).ToArray();

            Assert.AreEqual(
                triGold.Length,
                ts.Query<SUser>("users")
                    .Where(u => u.Active)
                    .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                    .Join<SLine>("lines", (u, o, l) => o.Id == l.OrderId)
                    .Where((u, o, l) => l.Qty >= 3 && l.Flag != 3)
                    .Count(),
                ctx + " 3join count");

            var joinWhereGold = (
                from u in gold.Users.Values
                join o in gold.Orders.Values on u.Id equals o.UserId
                where o.Amount >= 44
                select o.Amount).OrderBy(a => a).ToArray();
            Assert.AreEqual(
                joinWhereGold.Length,
                ts.Query<SUser>("users")
                    .Join<SOrder>("orders", (u, o) => u.Id == o.UserId)
                    .Where((u, o) => o.Amount >= 44)
                    .Count(),
                ctx + " join+where count");

            if (!deep)
                return;

            var joinGot = ts.Query<SUser>("users")
                .Where(u => u.Active)
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .ToList()
                .Select(p => (p.Outer.Code, p.Inner.Amount))
                .OrderBy(p => p.Code)
                .ThenBy(p => p.Amount)
                .ToArray();
            Assert.AreEqual(Dump(joinGold), Dump(joinGot), ctx + " join rows");

            var joinPageGold = (
                from u in gold.Users.Values
                join o in gold.Orders.Values on u.Id equals o.UserId
                where u.Active && o.Status == 1
                orderby o.Id
                select (u.Code, o.Amount)).Skip(3).Take(4).ToArray();
            var joinPageGot = ts.Query<SUser>("users")
                .Where(u => u.Active)
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .OrderBy((u, o) => o.Id)
                .Skip(3)
                .Take(4)
                .ToList()
                .Select(p => (p.Outer.Code, p.Inner.Amount))
                .ToArray();
            Assert.AreEqual(Dump(joinPageGold), Dump(joinPageGot), ctx + " join skip/take");

            var joinWhereGot = ts.Query<SUser>("users")
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId)
                .Where((u, o) => o.Amount >= 44)
                .ToList()
                .Select(p => p.Inner.Amount)
                .OrderBy(a => a)
                .ToArray();
            Assert.AreEqual(Dump(joinWhereGold), Dump(joinWhereGot), ctx + " join+where");

            var triGot = ts.Query<SUser>("users")
                .Where(u => u.Active)
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Join<SLine>("lines", (u, o, l) => o.Id == l.OrderId)
                .Where((u, o, l) => l.Qty >= 3 && l.Flag != 3)
                .ToList()
                .Select(r => (r.A.Code, r.B.Amount, r.C.Qty, r.C.Flag))
                .OrderBy(t => t.Code)
                .ThenBy(t => t.Amount)
                .ThenBy(t => t.Qty)
                .ToArray();
            Assert.AreEqual(Dump(triGold), Dump(triGot), ctx + " 3join rows");

            var topQtyGold = (
                from u in gold.Users.Values
                join o in gold.Orders.Values on u.Id equals o.UserId
                join l in gold.Lines.Values on o.Id equals l.OrderId
                where o.Status == 1
                orderby (l.Qty * 10000 + u.Code) descending
                select l.Qty).Take(5).ToArray();
            var topQtyGot = ts.Query<SUser>("users")
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Join<SLine>("lines", (u, o, l) => o.Id == l.OrderId)
                .OrderByDescending((u, o, l) => l.Qty * 10000 + u.Code)
                .Take(5)
                .ToList()
                .Select(r => r.C.Qty)
                .ToArray();
            Assert.AreEqual(Dump(topQtyGold), Dump(topQtyGot), ctx + " 3join take");

            // Nested-loop join is O(N×M) id probes (not entity new()). Sparse checkpoints only.
            var nestedGoldN = gold.Users.Values.Where(u => u.Code % 11 == 0)
                .Sum(u => gold.Orders.Values.Count(o => u.Score < o.Amount));
            Assert.AreEqual(
                nestedGoldN,
                ts.Query<SUser>("users")
                    .Where(u => u.Code % 11 == 0)
                    .Join<SOrder>("orders", (u, o) => u.Score < o.Amount)
                    .Count(),
                ctx + " nested count");

            if (step >= 0)
                return;

            var nestedGold = (
                from u in gold.Users.Values
                where u.Code % 11 == 0
                from o in gold.Orders.Values
                where u.Score < o.Amount
                select (u.Code, o.Amount)).OrderBy(p => p.Code).ThenBy(p => p.Amount).ToArray();
            var nestedGot = ts.Query<SUser>("users")
                .Where(u => u.Code % 11 == 0)
                .Join<SOrder>("orders", (u, o) => u.Score < o.Amount)
                .ToList()
                .Select(p => (p.Outer.Code, p.Inner.Amount))
                .OrderBy(p => p.Code)
                .ThenBy(p => p.Amount)
                .ToArray();
            Assert.AreEqual(Dump(nestedGold), Dump(nestedGot), ctx + " nested rows");
        }

        private static string Dump<T>(IEnumerable<T> xs) => string.Join("|", xs);

        [TestMethod]
        public void JoinAndExists_DoNotMaterializeInner_OnCountAndWindow()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create("users", [("Code", DbValueType.Int, true)]);
            ts.Create("orders", [("UserId", DbValueType.UInt, false), ("Amount", DbValueType.Int, false)]);

            var ids = new uint[400];
            for (int i = 0; i < 400; i++)
            {
                ids[i] = ts.Insert_Entity("users", new CountUser { Code = i }).Value;
                ts.Insert_Entity("orders", new CountOrder { UserId = ids[i], Amount = 10 + i });
                ts.Insert_Entity("orders", new CountOrder { UserId = ids[i], Amount = 1000 + i });
            }

            CountUser.Reset();
            CountOrder.Reset();
            var n = ts.Query_Entity<CountUser>("users")
                .JoinEntity<CountOrder>("orders", (u, o) => u.Id == o.UserId && o.Amount >= 1000)
                .Count();
            Assert.AreEqual(400, n);
            Assert.AreEqual(0, CountUser.Reads, "Join Count must not unbox users");
            Assert.AreEqual(0, CountOrder.Reads, "Join Count must not unbox orders");

            CountUser.Reset();
            CountOrder.Reset();
            var exists = ts.Query_Entity<CountUser>("users")
                .WhereExists<CountOrder>("orders", (u, o) => u.Id == o.UserId && o.Amount >= 1000)
                .Count();
            Assert.AreEqual(400, exists);
            Assert.AreEqual(0, CountUser.Reads);
            Assert.AreEqual(0, CountOrder.Reads);

            CountUser.Reset();
            CountOrder.Reset();
            var page = ts.Query_Entity<CountUser>("users")
                .JoinEntity<CountOrder>("orders", (u, o) => u.Id == o.UserId && o.Amount >= 1000)
                .OrderBy((u, o) => o.Amount)
                .Take(4)
                .ToList();
            Assert.AreEqual(4, page.Count);
            Assert.AreEqual(4, CountUser.Reads, "only window users");
            Assert.AreEqual(4, CountOrder.Reads, "only window orders");

            CountUser.Reset();
            CountOrder.Reset();
            var exPage = ts.Query_Entity<CountUser>("users")
                .WhereExists<CountOrder>("orders", (u, o) => u.Id == o.UserId && o.Amount >= 1000)
                .Take(5)
                .ToList();
            Assert.AreEqual(5, exPage.Count);
            Assert.AreEqual(5, CountUser.Reads);
            Assert.AreEqual(0, CountOrder.Reads, "EXISTS must not unbox inner");
        }

        [TestMethod]
        public void CapturedCustomDelegate_NotAction()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<SUser>("users");
            ts.Insert("users", new SUser
            {
                Code = 1,
                Score = 20,
                Active = true,
                Tag = "ab",
                When = DateTime.UtcNow,
            });
            ts.Insert("users", new SUser
            {
                Code = 2,
                Score = 1,
                Active = false,
                Tag = "x",
                When = DateTime.UtcNow,
            });

            MixPred mix = static (s, tag, a) => a && s >= 10 && tag.Contains('a');
            var n = ts.Query<SUser>("users").Where(u => mix(u.Score, u.Tag, u.Active)).Count();
            Assert.AreEqual(1, n);

            Func<int, string, bool, DateTime, bool> four = static (s, tag, a, w) =>
                a && s >= 10 && tag.Length == 2 && w.Year >= 2020;
            Assert.AreEqual(1, ts.Query<SUser>("users").Where(u => four(u.Score, u.Tag, u.Active, u.When)).Count());

            Func<DateTime, bool> after2020 = static w => w.Year >= 2020;
            Assert.AreEqual(2, ts.Query<SUser>("users").Where(u => after2020(u.When)).Count());

            try
            {
                ts.Query<SUser>("users").Where(u => u.Tag.Split('a').Length > 0).ToList();
                Assert.Fail("Split still must not translate");
            }
            catch (LumException)
            {
            }
        }

        [TestMethod]
        public void DeterministicDrill_JoinExists_Empty_OneToMany_Orphans()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create<SUser>("users");
            ts.Create<SOrder>("orders");
            ts.Create<SLine>("lines");
            ts.Create<SOrder>("empty_orders");

            var t0 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var uids = new uint[12];
            for (int i = 0; i < 12; i++)
            {
                uids[i] = ts.Insert("users", new SUser
                {
                    Code = i,
                    Score = i * 7 - 20,
                    Active = i % 3 != 0,
                    Val = i + 0.5,
                    Tag = "t" + (i % 5),
                    When = t0.AddHours(i),
                }).Value;
            }

            // user 0: five matching orders (1-to-many); user 1: none; user 2: mixed status
            for (int k = 0; k < 5; k++)
            {
                var oid = ts.Insert("orders", new SOrder
                {
                    UserId = uids[0],
                    Status = 1,
                    Amount = 40 + k,
                    Label = "T0",
                }).Value;
                ts.Insert("lines", new SLine { OrderId = oid, Qty = k + 1, Flag = (byte)(k % 4) });
            }

            ts.Insert("orders", new SOrder { UserId = uids[2], Status = 1, Amount = 9, Label = "T2" });
            ts.Insert("orders", new SOrder { UserId = uids[2], Status = 0, Amount = 90, Label = "T2" });
            var deadOrder = ts.Insert("orders", new SOrder { UserId = uids[4], Status = 1, Amount = 33, Label = "T4" }).Value;
            ts.Insert("lines", new SLine { OrderId = deadOrder, Qty = 8, Flag = 1 });

            Assert.AreEqual(0, ts.Query<SUser>("users")
                .Join<SOrder>("empty_orders", (u, o) => u.Id == o.UserId)
                .Count());
            Assert.AreEqual(0, ts.Query<SUser>("users")
                .WhereExists<SOrder>("empty_orders", (u, o) => u.Id == o.UserId)
                .Count());

            Assert.AreEqual(5, ts.Query<SUser>("users")
                .Where(u => u.Code == 0)
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Count());
            Assert.AreEqual(1, ts.Query<SUser>("users")
                .Where(u => u.Code == 0)
                .WhereExists<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Count());
            Assert.AreEqual(0, ts.Query<SUser>("users")
                .Where(u => u.Code == 1)
                .WhereExists<SOrder>("orders", (u, o) => u.Id == o.UserId)
                .Count());

            var many = ts.Query<SUser>("users")
                .Where(u => u.Code == 0)
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .OrderBy((u, o) => o.Amount)
                .ToList();
            Assert.AreEqual(5, many.Count);
            CollectionAssert.AreEqual(new[] { 40, 41, 42, 43, 44 }, many.Select(p => p.Inner.Amount).ToArray());

            var tri = ts.Query<SUser>("users")
                .Where(u => u.Code == 0)
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId && o.Status == 1)
                .Join<SLine>("lines", (u, o, l) => o.Id == l.OrderId && l.Qty >= 3)
                .OrderBy((u, o, l) => l.Qty)
                .ToList();
            Assert.AreEqual(3, tri.Count);
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, tri.Select(r => r.C.Qty).ToArray());

            Assert.AreEqual(1, ts.Query<SOrder>("orders").Where(o => o.Id == deadOrder).Delete());
            Assert.AreEqual(0, ts.Query<SUser>("users")
                .Where(u => u.Code == 4)
                .WhereExists<SOrder>("orders", (u, o) => u.Id == o.UserId)
                .Count());
            // orphan line stays on disk; inner join must drop it
            Assert.AreEqual(0, ts.Query<SUser>("users")
                .Join<SOrder>("orders", (u, o) => u.Id == o.UserId)
                .Join<SLine>("lines", (u, o, l) => o.Id == l.OrderId && l.Qty == 8)
                .Count());
            Assert.AreEqual(1, ts.Query<SLine>("lines").Where(l => l.Qty == 8).Count());

            var ineq = ts.Query<SUser>("users")
                .Where(u => u.Code == 2)
                .Join<SOrder>("orders", (u, o) => u.Score < o.Amount && o.Status == 0)
                .ToList();
            Assert.AreEqual(1, ineq.Count);
            Assert.AreEqual(90, ineq[0].Inner.Amount);

            MixPred mix = static (s, tag, a) => a && s > 0 && tag.StartsWith("t");
            var mixN = ts.Query<SUser>("users").Where(u => mix(u.Score, u.Tag, u.Active)).Count();
            var goldMix = Enumerable.Range(0, 12).Count(i => (i % 3 != 0) && (i * 7 - 20) > 0);
            Assert.AreEqual(goldMix, mixN);
        }

        [TestMethod]
        public void NestedLoop_Count_DoesNotUnboxEntities()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create("users", [("Code", DbValueType.Int, true)]);
            ts.Create("orders", [("UserId", DbValueType.UInt, false), ("Amount", DbValueType.Int, false)]);
            for (int i = 0; i < 80; i++)
            {
                var id = ts.Insert_Entity("users", new CountUser { Code = i }).Value;
                ts.Insert_Entity("orders", new CountOrder { UserId = id, Amount = 50 + (i % 9) });
            }

            CountUser.Reset();
            CountOrder.Reset();
            var n = ts.Query_Entity<CountUser>("users")
                .Where(u => u.Code % 10 == 0)
                .JoinEntity<CountOrder>("orders", (u, o) => u.Code < o.Amount)
                .Count();
            Assert.IsTrue(n > 0);
            Assert.AreEqual(0, CountUser.Reads, "nested-loop Count must not unbox left");
            Assert.AreEqual(0, CountOrder.Reads, "nested-loop Count must not unbox inner");
        }
    }
}
