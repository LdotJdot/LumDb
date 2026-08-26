using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Complex DataVar scenarios targeting hole reuse, shared continuation pages,
    /// and interleaved multi-column var churn. All seeds are fixed for reproducibility.
    /// </summary>
    [TestClass]
    public class DataVarHoleAndComplexScenarioTests
    {
        private const string Table = "var_complex";

        private static string S(int id, int len, int salt)
        {
            var rng = new Random(HashCode.Combine(id, len, salt));
            var chars = new char[len];
            for (int i = 0; i < len; i++)
                chars[i] = (char)('A' + rng.Next(0, 26));
            return new string(chars);
        }

        private static byte[] B(int id, int len, int salt)
        {
            var rng = new Random(HashCode.Combine(id, len, salt, 99));
            var bytes = new byte[len];
            rng.NextBytes(bytes);
            return bytes;
        }

        private sealed class Expect
        {
            public string Body = "";
            public byte[] Bin = [];
            public string Note = "";
        }

        private static void CreateDual(ITransaction ts) =>
            Assert.IsTrue(ts.Create(Table, [
                ("id", DbValueType.Int, true),
                ("body", DbValueType.StrVar, false),
                ("bin", DbValueType.BytesVar, false),
            ]).IsSuccess);

        private static void CreateTriple(ITransaction ts) =>
            Assert.IsTrue(ts.Create(Table, [
                ("id", DbValueType.Int, true),
                ("body", DbValueType.StrVar, false),
                ("bin", DbValueType.BytesVar, false),
                ("note", DbValueType.StrVar, false),
            ]).IsSuccess);

        private static void AssertMap(ITransaction ts, IReadOnlyDictionary<int, Expect> map, string phase, bool triple)
        {
            foreach (var (id, e) in map)
            {
                var f = ts.Find(Table, "id", id);
                Assert.IsTrue(f.IsSuccess, $"{phase} id={id}: {f.Exception?.Message}");
                Assert.AreEqual(e.Body, f.Row.GetString(1), $"{phase} body id={id}");
                CollectionAssert.AreEqual(e.Bin, f.Row.GetBytes(2), $"{phase} bin id={id}");
                if (triple)
                    Assert.AreEqual(e.Note, f.Row.GetString(3), $"{phase} note id={id}");
            }

            int n = 0;
            ts.GoThrough(Table, (ref RowView _) => { n++; return true; });
            Assert.AreEqual(map.Count, n, $"{phase} count");
        }

        /// <summary>
        /// Fill with long (multi-page) + short, delete longs to create holes on shared pages,
        /// rewrite short into same ids (should hit single-node hole reuse when available).
        /// </summary>
        [TestMethod]
        public void Hole_Seed801_LongDelete_ShortRewrite_Reopen()
        {
            const int seed = 801;
            var path = Configuration.GetRandomPath();
            var map = new Dictionary<int, Expect>();
            var rng = new Random(seed);

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateDual(ts);

                for (int i = 0; i < 100; i++)
                {
                    bool isLong = i % 2 == 0;
                    int bl = isLong ? rng.Next(4500, 9000) : rng.Next(8, 80);
                    int bn = isLong ? rng.Next(3000, 7000) : rng.Next(2, 40);
                    var e = new Expect { Body = S(i, bl, seed), Bin = B(i, bn, seed) };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess);
                    map[i] = e;
                }

                for (int i = 0; i < 100; i += 2)
                {
                    Assert.IsTrue(ts.Delete(Table, "id", i).IsSuccess);
                    map.Remove(i);
                }

                // Rewrite deleted ids with short payloads (fit hole SpaceLength of former long first-chunk)
                for (int i = 0; i < 100; i += 2)
                {
                    int bl = rng.Next(10, 100);
                    int bn = rng.Next(4, 50);
                    var e = new Expect { Body = S(i, bl, seed + 1), Bin = B(i, bn, seed + 1) };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess);
                    map[i] = e;
                }

                AssertMap(ts, map, "same", triple: false);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertMap(ts, map, "reopen", triple: false);
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Shared continuation: long spans pages, shorts append on continuation page,
        /// truncate long in-place, verify shorts still readable (H8/H9 regression).
        /// </summary>
        [TestMethod]
        public void SharedContinuation_Seed802_TruncateLong_ShortsSurvive_Reopen()
        {
            const int seed = 802;
            var path = Configuration.GetRandomPath();
            var map = new Dictionary<int, Expect>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateDual(ts);

                for (int i = 0; i < 40; i++)
                {
                    var e = new Expect
                    {
                        Body = S(i, i == 10 ? 12000 : 20, seed),
                        Bin = B(i, i == 11 ? 9000 : 8, seed),
                    };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess);
                    map[i] = e;
                }

                // More shorts after multi-page values so continuation pages gain neighbors
                for (int i = 40; i < 200; i++)
                {
                    var e = new Expect { Body = S(i, 24, seed), Bin = B(i, 6, seed) };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess);
                    map[i] = e;
                }

                // Truncate the long ones
                map[10].Body = S(10, 40, seed + 9);
                map[11].Bin = B(11, 16, seed + 9);
                Assert.IsTrue(ts.Update(Table, "id", 10, "body", map[10].Body).IsSuccess);
                Assert.IsTrue(ts.Update(Table, "id", 11, "bin", map[11].Bin).IsSuccess);

                AssertMap(ts, map, "after-trunc", triple: false);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertMap(ts, map, "reopen", triple: false);
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Grow beyond capacity forces delete+reinsert; then shrink; interleaved with neighbors.
        /// </summary>
        [TestMethod]
        public void GrowBeyondCapacity_Seed803_ThenShrink_NeighborsOk_Reopen()
        {
            const int seed = 803;
            var path = Configuration.GetRandomPath();
            var map = new Dictionary<int, Expect>();
            var rng = new Random(seed);

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTriple(ts);

                for (int i = 0; i < 60; i++)
                {
                    var e = new Expect
                    {
                        Body = S(i, 100, seed),
                        Bin = B(i, 80, seed),
                        Note = S(i, 60, seed + 3),
                    };
                    Assert.IsTrue(ts.Insert(Table, [
                        ("id", i), ("body", e.Body), ("bin", e.Bin), ("note", e.Note),
                    ]).IsSuccess);
                    map[i] = e;
                }

                for (int round = 0; round < 80; round++)
                {
                    int id = rng.Next(0, 60);
                    int bl = rng.Next(2000, 11000);
                    map[id].Body = S(id, bl, seed + round);
                    Assert.IsTrue(ts.Update(Table, "id", id, "body", map[id].Body).IsSuccess);

                    int id2 = (id + 17) % 60;
                    map[id2].Note = S(id2, rng.Next(10, 40), seed + round + 1);
                    Assert.IsTrue(ts.Update(Table, "id", id2, "note", map[id2].Note).IsSuccess);
                }

                for (int id = 0; id < 60; id += 3)
                {
                    map[id].Body = S(id, 15, seed + 999);
                    map[id].Bin = B(id, 12, seed + 999);
                    Assert.IsTrue(ts.Update(Table, "id", id, "body", map[id].Body).IsSuccess);
                    Assert.IsTrue(ts.Update(Table, "id", id, "bin", map[id].Bin).IsSuccess);
                }

                AssertMap(ts, map, "same", triple: true);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertMap(ts, map, "reopen", triple: true);
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Heavy delete of shorts leaving longs, then rewrite shorts with varying sizes;
        /// mid-session SaveChanges + reopen between phases.
        /// </summary>
        [TestMethod]
        public void Phased_Seed804_DeleteShort_Rewrite_MixedSizes_MultiReopen()
        {
            const int seed = 804;
            var path = Configuration.GetRandomPath();
            var map = new Dictionary<int, Expect>();
            var rng = new Random(seed);

            void PhaseWrite(ITransaction ts, int from, int to, int salt)
            {
                for (int i = from; i < to; i++)
                {
                    bool longish = i % 5 == 0;
                    var e = new Expect
                    {
                        Body = S(i, longish ? rng.Next(5000, 8000) : rng.Next(5, 60), salt),
                        Bin = B(i, longish ? rng.Next(2000, 5000) : rng.Next(2, 30), salt),
                    };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess, $"ins {i}");
                    map[i] = e;
                }
            }

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateDual(ts);
                PhaseWrite(ts, 0, 120, seed);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            using (var ts = eng.StartTransaction())
            {
                AssertMap(ts, map, "reopen1", triple: false);

                for (int i = 0; i < 120; i++)
                {
                    if (i % 5 != 0)
                    {
                        Assert.IsTrue(ts.Delete(Table, "id", i).IsSuccess);
                        map.Remove(i);
                    }
                }

                for (int i = 0; i < 120; i++)
                {
                    if (map.ContainsKey(i)) continue;
                    int bl = rng.Next(0, 4) switch
                    {
                        0 => rng.Next(5, 40),
                        1 => rng.Next(200, 800),
                        2 => rng.Next(3000, 6000),
                        _ => rng.Next(8000, 12000),
                    };
                    var e = new Expect { Body = S(i, bl, seed + 2), Bin = B(i, Math.Max(2, bl / 3), seed + 2) };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess);
                    map[i] = e;
                }

                AssertMap(ts, map, "phase2", triple: false);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertMap(ts, map, "reopen2", triple: false);
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Seeded 2500-step fuzz: biased toward delete+insert (hole pressure) and grow/shrink.
        /// </summary>
        [TestMethod]
        public void Fuzz_Seed805_2500Steps_HoleBiased_TripleVar_Reopen()
        {
            const int seed = 805;
            const int steps = 2500;
            const int idRange = 350;
            var path = Configuration.GetRandomPath();
            var rng = new Random(seed);
            var map = new Dictionary<int, Expect>();

            int Len() => rng.Next(0, 5) switch
            {
                0 => rng.Next(2, 20),
                1 => rng.Next(40, 150),
                2 => rng.Next(400, 1500),
                3 => rng.Next(4000, 7500),
                _ => rng.Next(9000, 14000),
            };

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTriple(ts);

                for (int i = 0; i < 30; i++)
                {
                    int id = i * 11 + 3;
                    var e = new Expect
                    {
                        Body = S(id, Len(), seed),
                        Bin = B(id, Len() / 2 + 2, seed),
                        Note = S(id, Len() / 3 + 4, seed + 1),
                    };
                    if (ts.Insert(Table, [("id", id), ("body", e.Body), ("bin", e.Bin), ("note", e.Note)]).IsSuccess)
                        map[id] = e;
                }

                for (int step = 0; step < steps; step++)
                {
                    int id = rng.Next(0, idRange);
                    // Bias: 0,1 delete; 2,3 insert; 4 update grow; 5 shrink; 6 update both; 7 update note
                    int op = rng.Next(0, 8);

                    switch (op)
                    {
                        case 0:
                        case 1:
                            if (map.ContainsKey(id) && ts.Delete(Table, "id", id).IsSuccess)
                                map.Remove(id);
                            break;
                        case 2:
                        case 3:
                            if (!map.ContainsKey(id))
                            {
                                var e = new Expect
                                {
                                    Body = S(id, Len(), seed + step),
                                    Bin = B(id, Len() / 2 + 1, seed + step),
                                    Note = S(id, Len() / 4 + 2, seed + step),
                                };
                                if (ts.Insert(Table, [("id", id), ("body", e.Body), ("bin", e.Bin), ("note", e.Note)]).IsSuccess)
                                    map[id] = e;
                            }
                            break;
                        case 4:
                            if (map.TryGetValue(id, out var g))
                            {
                                g.Body = S(id, Math.Max(g.Body.Length + 500, Len()), seed + step);
                                if (ts.Update(Table, "id", id, "body", g.Body).IsSuccess)
                                    map[id] = g;
                            }
                            break;
                        case 5:
                            if (map.TryGetValue(id, out var sh))
                            {
                                sh.Body = S(id, rng.Next(2, 50), seed + step);
                                sh.Bin = B(id, rng.Next(2, 40), seed + step);
                                if (ts.Update(Table, "id", id, "body", sh.Body).IsSuccess &&
                                    ts.Update(Table, "id", id, "bin", sh.Bin).IsSuccess)
                                    map[id] = sh;
                            }
                            break;
                        case 6:
                            if (map.TryGetValue(id, out var u))
                            {
                                u.Body = S(id, Len(), seed + step);
                                u.Bin = B(id, Len() / 2 + 1, seed + step);
                                if (ts.Update(Table, "id", id, "body", u.Body).IsSuccess &&
                                    ts.Update(Table, "id", id, "bin", u.Bin).IsSuccess)
                                    map[id] = u;
                            }
                            break;
                        case 7:
                            if (map.TryGetValue(id, out var n))
                            {
                                n.Note = S(id, Len(), seed + step);
                                if (ts.Update(Table, "id", id, "note", n.Note).IsSuccess)
                                    map[id] = n;
                            }
                            break;
                    }

                    if ((step + 1) % 500 == 0)
                        AssertMap(ts, map, $"step{step + 1}", triple: true);
                }

                AssertMap(ts, map, "final", triple: true);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertMap(ts, map, "reopen", triple: true);
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Same id repeatedly: insert long → delete → insert short → delete → insert long…
        /// Forces hole create/reuse cycles on one key.
        /// </summary>
        [TestMethod]
        public void Cycle_Seed806_SameId_LongShort_DeleteRewrite_50Rounds_Reopen()
        {
            const int seed = 806;
            var path = Configuration.GetRandomPath();
            var rng = new Random(seed);
            Expect? last = null;

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateDual(ts);

                // Neighbors to keep pages shared
                for (int i = 1; i <= 30; i++)
                {
                    Assert.IsTrue(ts.Insert(Table, [
                        ("id", i),
                        ("body", S(i, 30, seed)),
                        ("bin", B(i, 10, seed)),
                    ]).IsSuccess);
                }

                for (int round = 0; round < 50; round++)
                {
                    if (last != null)
                        Assert.IsTrue(ts.Delete(Table, "id", 0).IsSuccess);

                    bool isLong = round % 2 == 0;
                    last = new Expect
                    {
                        Body = S(0, isLong ? rng.Next(5000, 10000) : rng.Next(5, 80), seed + round),
                        Bin = B(0, isLong ? rng.Next(2000, 6000) : rng.Next(2, 40), seed + round),
                    };
                    Assert.IsTrue(ts.Insert(Table, [("id", 0), ("body", last.Body), ("bin", last.Bin)]).IsSuccess);

                    var f = ts.Find(Table, "id", 0);
                    Assert.IsTrue(f.IsSuccess, $"round {round}: {f.Exception?.Message}");
                    Assert.AreEqual(last.Body, f.Row.GetString(1));
                    CollectionAssert.AreEqual(last.Bin, f.Row.GetBytes(2));
                }

                for (int i = 1; i <= 30; i++)
                {
                    var f = ts.Find(Table, "id", i);
                    Assert.IsTrue(f.IsSuccess);
                    Assert.AreEqual(S(i, 30, seed), f.Row.GetString(1));
                }

                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                var f = ts.Find(Table, "id", 0);
                Assert.IsTrue(f.IsSuccess);
                Assert.AreEqual(last!.Body, f.Row.GetString(1));
                CollectionAssert.AreEqual(last.Bin, f.Row.GetBytes(2));
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Parallel columns: update only body while bin stays long multi-page; then swap.
        /// </summary>
        [TestMethod]
        public void DualColumn_Seed807_IndependentUpdate_Reopen()
        {
            const int seed = 807;
            var path = Configuration.GetRandomPath();
            var map = new Dictionary<int, Expect>();
            var rng = new Random(seed);

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateDual(ts);

                for (int i = 0; i < 50; i++)
                {
                    var e = new Expect
                    {
                        Body = S(i, rng.Next(4000, 7000), seed),
                        Bin = B(i, rng.Next(4000, 7000), seed),
                    };
                    Assert.IsTrue(ts.Insert(Table, [("id", i), ("body", e.Body), ("bin", e.Bin)]).IsSuccess);
                    map[i] = e;
                }

                for (int i = 0; i < 50; i++)
                {
                    map[i].Body = S(i, rng.Next(10, 40), seed + 1);
                    Assert.IsTrue(ts.Update(Table, "id", i, "body", map[i].Body).IsSuccess);
                }

                AssertMap(ts, map, "body-shrunk", triple: false);

                for (int i = 0; i < 50; i++)
                {
                    map[i].Bin = B(i, rng.Next(10, 40), seed + 2);
                    Assert.IsTrue(ts.Update(Table, "id", i, "bin", map[i].Bin).IsSuccess);
                }

                AssertMap(ts, map, "both-shrunk", triple: false);

                for (int i = 0; i < 50; i++)
                {
                    map[i].Body = S(i, rng.Next(6000, 10000), seed + 3);
                    map[i].Bin = B(i, rng.Next(6000, 10000), seed + 3);
                    Assert.IsTrue(ts.Update(Table, "id", i, "body", map[i].Body).IsSuccess);
                    Assert.IsTrue(ts.Update(Table, "id", i, "bin", map[i].Bin).IsSuccess);
                }

                AssertMap(ts, map, "regrown", triple: false);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertMap(ts, map, "reopen", triple: false);
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
