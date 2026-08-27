using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    /// <summary>
    /// Seeded long-flow mixed CRUD + LINQ windows. Occupancy order comes from GoThrough
    /// (page walk); shadow map is value truth. Reproducible via seed.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EntityLinqSeededFlowTests
    {
        private const string Hand = "hand";
        private const string Gen = "gen";

        [TestMethod]
        [DataRow(20260826, 420, false)]
        [DataRow(1, 280, false)]
        [DataRow(99991, 360, true)]
        [DataRow(42, 200, true)]
        public void Seeded_MixedCrud_LinqWindows_MatchGoThrough(int seed, int steps, bool file)
        {
            Run(seed, steps, file);
        }

        private static void Run(int seed, int steps, bool file)
        {
            var rng = new Random(seed);
            DbEngine? eng = null;
            string? path = null;
            try
            {
                eng = Configuration.Create(file ? TestBackend.File : TestBackend.Memory, out path);
                using (var ts = eng.StartTransaction())
                {
                    ts.Create(Hand, [("code", DbValueType.Int, true), ("name", DbValueType.Str32B, false)]);
                    ts.Create<FlowGenRow>(Gen);
                }

                var shadow = new Dictionary<int, Live>();
                var ids = new List<uint>();
                int nextCode = 1;
                int verifyEvery = Math.Max(15, steps / 12);

                for (int step = 0; step < steps; step++)
                {
                    using (var ts = eng.StartTransaction())
                        ApplyOp(rng, ts, shadow, ids, ref nextCode);

                    if (step > 0 && step % verifyEvery == 0)
                    {
                        using (var ts = eng.StartTransaction())
                            Verify(ts, shadow, rng, $"seed={seed} step={step}");
                    }

                    if (file && step > 0 && step % 80 == 0)
                    {
                        eng.Dispose();
                        eng = Configuration.GetDbEngineForTest(path!);
                    }
                }

                using (var ts = eng.StartTransaction())
                    Verify(ts, shadow, rng, $"seed={seed} final");

                using (var ro = eng.StartTransactionReadonly())
                    VerifyReadonly(ro, shadow, $"seed={seed} readonly");
            }
            finally
            {
                Configuration.Cleanup(file ? TestBackend.File : TestBackend.Memory, eng, path);
            }
        }

        private enum Op : byte
        {
            Insert = 0,
            Delete = 1,
            Update = 2,
            BurstInsert = 3,
            BurstDelete = 4,
        }

        private static void ApplyOp(Random rng, ITransaction ts, Dictionary<int, Live> shadow, List<uint> ids, ref int nextCode)
        {
            var op = shadow.Count == 0
                ? Op.Insert
                : (Op)rng.Next(0, 5);

            switch (op)
            {
                case Op.Insert:
                    InsertOne(ts, shadow, ids, ref nextCode, rng);
                    break;
                case Op.BurstInsert:
                    int n = rng.Next(2, 8);
                    for (int i = 0; i < n; i++)
                        InsertOne(ts, shadow, ids, ref nextCode, rng);
                    break;
                case Op.Delete:
                    DeleteOne(rng, ts, shadow, ids);
                    break;
                case Op.BurstDelete:
                    int d = rng.Next(1, Math.Min(6, ids.Count + 1));
                    for (int i = 0; i < d && ids.Count > 0; i++)
                        DeleteOne(rng, ts, shadow, ids);
                    break;
                case Op.Update:
                    if (ids.Count == 0)
                    {
                        InsertOne(ts, shadow, ids, ref nextCode, rng);
                        break;
                    }
                    var id = ids[rng.Next(ids.Count)];
                    var live = shadow.Values.First(x => x.Id == id);
                    var name = RandName(rng);
                    Assert.IsTrue(ts.Update_Entity(Hand, id, new FlowHandRow { Code = live.Code, Name = name }).IsSuccess);
                    Assert.IsTrue(ts.UpdateEntity(Gen, id, new FlowGenRow { Code = live.Code, Name = name }).IsSuccess);
                    live.Name = name;
                    break;
            }
        }

        private static void InsertOne(ITransaction ts, Dictionary<int, Live> shadow, List<uint> ids, ref int nextCode, Random rng)
        {
            int code = nextCode++;
            var name = RandName(rng);
            var hid = ts.Insert_Entity(Hand, new FlowHandRow { Code = code, Name = name });
            var gid = ts.Insert(Gen, new FlowGenRow { Code = code, Name = name });
            Assert.IsTrue(hid.IsSuccess && gid.IsSuccess);
            Assert.AreEqual(hid.Value, gid.Value, "lockstep tables must share engine ids");
            shadow[code] = new Live { Id = hid.Value, Code = code, Name = name };
            ids.Add(hid.Value);
        }

        private static void DeleteOne(Random rng, ITransaction ts, Dictionary<int, Live> shadow, List<uint> ids)
        {
            if (ids.Count == 0)
                return;
            int ix = rng.Next(ids.Count);
            uint id = ids[ix];
            ids.RemoveAt(ix);
            var live = shadow.Values.First(x => x.Id == id);
            shadow.Remove(live.Code);
            Assert.IsTrue(ts.Delete(Hand, id).IsSuccess);
            Assert.IsTrue(ts.Delete(Gen, id).IsSuccess);
        }

        private static void Verify(ITransaction ts, Dictionary<int, Live> shadow, Random rng, string tag)
        {
            var occ = Occupancy(ts, Hand);
            var occGen = Occupancy(ts, Gen);
            CollectionAssert.AreEqual(occ, occGen, $"{tag}: hand/gen occupancy diverged");
            Assert.AreEqual(shadow.Count, occ.Count, $"{tag}: occupancy count");

            foreach (var code in occ)
            {
                Assert.IsTrue(shadow.ContainsKey(code), $"{tag}: occupancy code {code} missing in shadow");
                var live = shadow[code];
                var byId = ts.Find_Entity<FlowHandRow>(Hand, live.Id);
                Assert.IsTrue(byId.IsSuccess, $"{tag}: find id {live.Id}");
                Assert.AreEqual(live.Name, byId.Value.Name, $"{tag}: name id {live.Id}");
                var byKey = ts.Find_Entity<FlowHandRow>(Hand, "code", live.Code);
                Assert.AreEqual(live.Name, byKey.Value.Name);
                var gen = ts.FindByIdEntity<FlowGenRow>(Gen, live.Id);
                Assert.AreEqual(live.Name, gen.Value.Name);
                Assert.AreEqual(live.Code, gen.Value.Code);
            }

            var linqAll = ts.Query_Entity<FlowHandRow>(Hand).ToValues().Values.Select(x => x.Code).ToArray();
            CollectionAssert.AreEqual(occ, linqAll, $"{tag}: Find_Entity identity vs GoThrough");

            var back = ts.Query_Entity<FlowHandRow>(Hand).Reverse().ToValues().Values.Select(x => x.Code).ToArray();
            CollectionAssert.AreEqual(occ.AsEnumerable().Reverse().ToArray(), back, $"{tag}: backward");

            var lumAll = ts.Find<FlowGenRow>(Gen, static (ref RowView _) => true).Values.Select(x => x.Code).ToArray();
            CollectionAssert.AreEqual(occ, lumAll, $"{tag}: LumEntity Find vs occupancy");

            int take = Math.Min(7, occ.Count);
            FlowHandRow.Reset();
            var taken = ts.Query_Entity<FlowHandRow>(Hand).Take(take).ToValues();
            CollectionAssert.AreEqual(occ.Take(take).ToArray(), taken.Values.Select(x => x.Code).ToArray(), $"{tag}: Take");
            Assert.AreEqual(take, FlowHandRow.Reads, $"{tag}: Take must not Unbox the whole table");

            int backTake = Math.Min(5, occ.Count);
            FlowHandRow.Reset();
            var backWin = ts.Query_Entity<FlowHandRow>(Hand).Reverse().Take(backTake).ToValues();
            CollectionAssert.AreEqual(occ.AsEnumerable().Reverse().Take(backTake).ToArray(), backWin.Values.Select(x => x.Code).ToArray(), $"{tag}: backward Take");
            Assert.AreEqual(backTake, FlowHandRow.Reads, $"{tag}: backward Take pull");

            var even = occ.Where(c => c % 2 == 0).ToArray();
            int skip = even.Length == 0 ? 0 : rng.Next(0, Math.Min(4, even.Length));
            int lim = Math.Min(5, Math.Max(0, even.Length - skip));
            var expected = even.Skip(skip).Take(lim).ToArray();
            FlowHandRow.Reset();
            var window = ts.Query_Entity<FlowHandRow>(Hand).Where(x => x.Code % 2 == 0).Skip(skip).Take(lim).ToValues();
            CollectionAssert.AreEqual(expected, window.Values.Select(x => x.Code).ToArray(), $"{tag}: Where Skip Take");
            if (lim == 0)
                Assert.AreEqual(0, FlowHandRow.Reads, $"{tag}: empty window");
            else
                Assert.AreEqual(lim, FlowHandRow.Reads, $"{tag}: Where Skip Take materializes the window only");

            var lumWin = ts.Find<FlowGenRow>(Gen, (ref RowView row) => row.GetInt(0) % 2 == 0, (uint)skip, (uint)lim);
            CollectionAssert.AreEqual(expected, lumWin.Values.Select(x => x.Code).ToArray(), $"{tag}: LumEntity skip/limit");

            var evenCount = ts.Count(Gen, (ref RowView row) => row.GetInt(0) % 2 == 0);
            Assert.AreEqual((uint)even.Length, evenCount.Value, $"{tag}: Count even");

            if (shadow.Count > 0)
            {
                var sample = shadow.Values.ElementAt(rng.Next(shadow.Count));
                Assert.AreEqual(sample.Name, ts.FindEntity<FlowGenRow>(Gen, "Code", sample.Code).Value.Name);
            }

            var fromCallback = new List<int>();
            ts.GoThrough_Entity<FlowHandRow>(Hand, row =>
            {
                fromCallback.Add(row.Code);
                return true;
            });
            CollectionAssert.AreEqual(occ, fromCallback, $"{tag}: GoThrough_Entity");
        }

        private static void VerifyReadonly(LumDbEngine.Element.Engine.Transaction.AsNoTracking.ITransactionReadonly ts, Dictionary<int, Live> shadow, string tag)
        {
            var occ = new List<int>();
            ts.GoThrough(Hand, (ref RowView row) =>
            {
                occ.Add(row.GetInt(0));
                return true;
            });
            Assert.AreEqual(shadow.Count, occ.Count, tag);
            var linq = ts.Query_Entity<FlowHandRow>(Hand).Take(Math.Min(4, occ.Count)).ToValues();
            CollectionAssert.AreEqual(occ.Take(linq.Values.Count).ToArray(), linq.Values.Select(x => x.Code).ToArray(), tag);
        }

        private static List<int> Occupancy(ITransaction ts, string table)
        {
            var list = new List<int>();
            ts.GoThrough(table, (ref RowView row) =>
            {
                list.Add(row.GetInt(0));
                return true;
            });
            return list;
        }

        private static string RandName(Random rng)
        {
            int len = rng.Next(1, 12);
            var chars = new char[len];
            for (int i = 0; i < len; i++)
                chars[i] = (char)('a' + rng.Next(0, 26));
            return new string(chars);
        }

        private sealed class Live
        {
            public uint Id;
            public int Code;
            public string Name = "";
        }
    }

    public class FlowHandRow : IDbEntity
    {
        public static int Reads;
        public static void Reset() => Reads = 0;

        public uint Id { get; set; }
        public int Code { get; set; }
        public string Name { get; set; } = "";

        public void WriteTo(ref RowWriter writer)
        {
            writer.WriteInt(Code);
            writer.WriteString(Name);
        }

        public bool TryReadFrom(IDbRow row)
        {
            Reads++;
            Code = row.GetInt(0);
            Name = row.GetString(1);
            return true;
        }

        public void GetId(uint id) => Id = id;
    }

    [LumEntity]
    public partial class FlowGenRow
    {
        [Id] public uint Id { get; set; }
        [Key] public int Code { get; set; }
        [Str32B] public string Name { get; set; } = "";
    }
}
