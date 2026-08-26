using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Seeded pseudo-random fuzz for StrVar/BytesVar: reproducible interleaved insert / update / delete.
    ///
    /// DataVar reuse today (engine, no hole-fill yet):
    /// - In-place update when new payload fits existing node SpaceLength / chain TotalDataRestLength.
    /// - Whole DataVarPage recycled when CurrentDataCount hits 0 (PageManager.RecyclePage).
    /// - Deleted nodes: IsAvailable=false only; Insert always appends via ExpandDataVarNode — page holes are NOT reused.
    ///
    /// Planned hole reuse (tests first, implement later — see RunFuzz summary comment at bottom):
    /// Phase 1: on delete, return SpaceLength+HEADER to RestPageSize; keep a per-page free-slot list (index, space).
    /// Phase 2: InsertDataVar best-fit into dead slot when SpaceLength >= max(len, REDUNDANCY_SIZE).
    /// Phase 3: optional page compaction when free ratio exceeds threshold.
    /// </summary>
    [TestClass]
    public class DataVarSeededFuzzTests
    {
        private const string Table = "var_fuzz";

        private enum SizeTier { Tiny, Short, Medium, Long, XLarge }

        private enum VarOp : byte
        {
            Insert = 0,
            Delete = 1,
            UpdateStr = 2,
            UpdateBytes = 3,
            UpdateBoth = 4,
            GrowStr = 5,
            ShrinkStr = 6,
            GrowBytes = 7,
            ShrinkBytes = 8,
        }

        private sealed class RowExpect
        {
            public int StrLen;
            public int BinLen;
            public string Body = "";
            public byte[] Bin = [];
        }

        private sealed class FuzzConfig
        {
            public required int Seed;
            public required int Steps;
            public required int IdRange;
            public bool TripleStrVar;
            public bool ReopenAfter;
            public int MidSaveEvery; // 0 = only at end
            public int VerifyEvery;  // 0 = only at end
        }

        private static int TierLength(Random rng, SizeTier tier) => tier switch
        {
            SizeTier.Tiny => rng.Next(2, 17),
            SizeTier.Short => rng.Next(50, 201),
            SizeTier.Medium => rng.Next(500, 2001),
            SizeTier.Long => rng.Next(4000, 8001),
            SizeTier.XLarge => rng.Next(10000, 15001),
            _ => 16,
        };

        private static SizeTier RandomTier(Random rng) => (SizeTier)rng.Next(0, 5);

        private static SizeTier GrowTier(Random rng, SizeTier current)
        {
            int next = Math.Min(4, (int)current + 1 + rng.Next(0, 2));
            return (SizeTier)next;
        }

        private static SizeTier ShrinkTier(Random rng, SizeTier current)
        {
            int next = Math.Max(0, (int)current - 1 - rng.Next(0, 2));
            return (SizeTier)next;
        }

        private static SizeTier TierFromLength(int len) =>
            len switch
            {
                < 17 => SizeTier.Tiny,
                < 201 => SizeTier.Short,
                < 2001 => SizeTier.Medium,
                < 8001 => SizeTier.Long,
                _ => SizeTier.XLarge,
            };

        private static string MakeStr(int id, int len, int seed)
        {
            if (len <= 0) return "";
            var rng = new Random(Hash(id, len, seed, 1));
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
                sb.Append((char)('a' + rng.Next(0, 26)));
            sb.Append(':').Append(id.ToString("X8"));
            var s = sb.ToString();
            return s.Length > len ? s[..len] : s.PadRight(len, 'z');
        }

        private static byte[] MakeBin(int id, int len, int seed)
        {
            if (len <= 0) return [];
            var rng = new Random(Hash(id, len, seed, 2));
            var bin = new byte[len];
            rng.NextBytes(bin);
            bin[0] = (byte)(id & 0xFF);
            bin[Math.Min(1, len - 1)] = (byte)((id >> 8) & 0xFF);
            return bin;
        }

        private static int Hash(int a, int b, int c, int d) => HashCode.Combine(a, b, c, d);

        private static RowExpect BuildRow(int id, int strLen, int binLen, int seed, int noteLen = 0)
        {
            return new RowExpect
            {
                StrLen = strLen,
                BinLen = binLen,
                Body = MakeStr(id, strLen, seed),
                Bin = MakeBin(id, binLen, seed),
            };
        }

        private static void CreateTable(ITransaction ts, bool tripleStrVar)
        {
            if (tripleStrVar)
            {
                Assert.IsTrue(ts.Create(Table, [
                    ("id", DbValueType.Int, true),
                    ("body", DbValueType.StrVar, false),
                    ("bin", DbValueType.BytesVar, false),
                    ("note", DbValueType.StrVar, false),
                ]).IsSuccess);
            }
            else
            {
                Assert.IsTrue(ts.Create(Table, [
                    ("id", DbValueType.Int, true),
                    ("body", DbValueType.StrVar, false),
                    ("bin", DbValueType.BytesVar, false),
                ]).IsSuccess);
            }
        }

        private static bool TryInsert(ITransaction ts, int id, RowExpect row, bool tripleStrVar)
        {
            if (tripleStrVar)
            {
                string note = MakeStr(id, Math.Max(8, row.StrLen / 3), Hash(id, row.StrLen, row.BinLen, 3));
                return ts.Insert(Table, [
                    ("id", id),
                    ("body", row.Body),
                    ("bin", row.Bin),
                    ("note", note),
                ]).IsSuccess;
            }

            return ts.Insert(Table, [
                ("id", id),
                ("body", row.Body),
                ("bin", row.Bin),
            ]).IsSuccess;
        }

        private static void AssertExpected(ITransaction ts, IReadOnlyDictionary<int, RowExpect> expected, string phase)
        {
            foreach (var (id, row) in expected)
            {
                var found = ts.Find(Table, "id", id);
                Assert.IsTrue(found.IsSuccess, $"{phase} find id={id}: {found.Exception?.Message}");
                Assert.AreEqual(row.Body, found.Row.GetString(1), $"{phase} body id={id}");
                CollectionAssert.AreEqual(row.Bin, found.Row.GetBytes(2), $"{phase} bin id={id}");
            }

            int count = 0;
            ts.GoThrough(Table, (ref RowView _) => { count++; return true; });
            Assert.AreEqual(expected.Count, count, $"{phase}: GoThrough count");
        }

        private static void RunFuzzCore(FuzzConfig cfg, Action<string, IReadOnlyDictionary<int, RowExpect>>? afterPhase = null)
        {
            var path = Configuration.GetRandomPath();
            var rng = new Random(cfg.Seed);
            var expected = new Dictionary<int, RowExpect>();

            void RunSteps(ITransaction ts, int steps, string phase)
            {
                for (int step = 0; step < steps; step++)
                {
                    int id = rng.Next(0, cfg.IdRange);
                    var op = (VarOp)rng.Next(0, 9);

                    switch (op)
                    {
                        case VarOp.Insert:
                            if (!expected.ContainsKey(id))
                            {
                                var st = TierLength(rng, RandomTier(rng));
                                var bt = TierLength(rng, RandomTier(rng));
                                var row = BuildRow(id, st, bt, cfg.Seed);
                                if (TryInsert(ts, id, row, cfg.TripleStrVar))
                                    expected[id] = row;
                            }
                            break;

                        case VarOp.Delete:
                            if (expected.ContainsKey(id) && ts.Delete(Table, "id", id).IsSuccess)
                                expected.Remove(id);
                            break;

                        case VarOp.UpdateStr:
                            if (expected.TryGetValue(id, out var rs))
                            {
                                int newLen = TierLength(rng, RandomTier(rng));
                                rs.StrLen = newLen;
                                rs.Body = MakeStr(id, newLen, cfg.Seed);
                                if (ts.Update(Table, "id", id, "body", rs.Body).IsSuccess)
                                    expected[id] = rs;
                            }
                            break;

                        case VarOp.UpdateBytes:
                            if (expected.TryGetValue(id, out var rb))
                            {
                                int newLen = TierLength(rng, RandomTier(rng));
                                rb.BinLen = newLen;
                                rb.Bin = MakeBin(id, newLen, cfg.Seed);
                                if (ts.Update(Table, "id", id, "bin", rb.Bin).IsSuccess)
                                    expected[id] = rb;
                            }
                            break;

                        case VarOp.UpdateBoth:
                            if (expected.TryGetValue(id, out var ru))
                            {
                                int sl = TierLength(rng, RandomTier(rng));
                                int bl = TierLength(rng, RandomTier(rng));
                                ru.StrLen = sl;
                                ru.BinLen = bl;
                                ru.Body = MakeStr(id, sl, cfg.Seed);
                                ru.Bin = MakeBin(id, bl, cfg.Seed);
                                bool ok = ts.Update(Table, "id", id, "body", ru.Body).IsSuccess
                                    && ts.Update(Table, "id", id, "bin", ru.Bin).IsSuccess;
                                if (ok) expected[id] = ru;
                            }
                            break;

                        case VarOp.GrowStr:
                            if (expected.TryGetValue(id, out var rg))
                            {
                                int newLen = TierLength(rng, GrowTier(rng, TierFromLength(rg.StrLen)));
                                rg.StrLen = newLen;
                                rg.Body = MakeStr(id, newLen, cfg.Seed);
                                if (ts.Update(Table, "id", id, "body", rg.Body).IsSuccess)
                                    expected[id] = rg;
                            }
                            break;

                        case VarOp.ShrinkStr:
                            if (expected.TryGetValue(id, out var rsh))
                            {
                                int newLen = TierLength(rng, ShrinkTier(rng, TierFromLength(rsh.StrLen)));
                                rsh.StrLen = newLen;
                                rsh.Body = MakeStr(id, newLen, cfg.Seed);
                                if (ts.Update(Table, "id", id, "body", rsh.Body).IsSuccess)
                                    expected[id] = rsh;
                            }
                            break;

                        case VarOp.GrowBytes:
                            if (expected.TryGetValue(id, out var rgb))
                            {
                                int newLen = TierLength(rng, GrowTier(rng, TierFromLength(rgb.BinLen)));
                                rgb.BinLen = newLen;
                                rgb.Bin = MakeBin(id, newLen, cfg.Seed);
                                if (ts.Update(Table, "id", id, "bin", rgb.Bin).IsSuccess)
                                    expected[id] = rgb;
                            }
                            break;

                        case VarOp.ShrinkBytes:
                            if (expected.TryGetValue(id, out var rsb))
                            {
                                int newLen = TierLength(rng, ShrinkTier(rng, TierFromLength(rsb.BinLen)));
                                rsb.BinLen = newLen;
                                rsb.Bin = MakeBin(id, newLen, cfg.Seed);
                                if (ts.Update(Table, "id", id, "bin", rsb.Bin).IsSuccess)
                                    expected[id] = rsb;
                            }
                            break;
                    }

                    if (cfg.VerifyEvery > 0 && (step + 1) % cfg.VerifyEvery == 0)
                        AssertExpected(ts, expected, $"{phase}-step{step + 1}");
                }
            }

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts, cfg.TripleStrVar);

                // Bootstrap: ensure some rows exist before pure random churn
                for (int i = 0; i < 20; i++)
                {
                    int id = i * 13 + 7;
                    var row = BuildRow(id,
                        TierLength(rng, RandomTier(rng)),
                        TierLength(rng, RandomTier(rng)),
                        cfg.Seed);
                    if (TryInsert(ts, id, row, cfg.TripleStrVar))
                        expected[id] = row;
                }

                if (cfg.MidSaveEvery > 0)
                {
                    int done = 0;
                    while (done < cfg.Steps)
                    {
                        int batch = Math.Min(cfg.MidSaveEvery, cfg.Steps - done);
                        RunSteps(ts, batch, $"batch-{done}");
                        AssertExpected(ts, expected, $"pre-save-{done}");
                        ts.SaveChanges();
                        done += batch;
                    }
                }
                else
                {
                    RunSteps(ts, cfg.Steps, "main");
                }

                AssertExpected(ts, expected, "pre-final-save");
                ts.SaveChanges();
                afterPhase?.Invoke("same-engine", expected);
            }

            if (cfg.ReopenAfter)
            {
                using var eng = new DbEngine(path, createIfNotExists: false);
                using var ts = eng.StartTransaction();
                AssertExpected(ts, expected, "reopen");
                afterPhase?.Invoke("reopen", expected);
                eng.SetDestoryOnDisposed();
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Fuzz_Seed1001_600Steps_DualVar_Reopen()
        {
            RunFuzzCore(new FuzzConfig
            {
                Seed = 1001,
                Steps = 600,
                IdRange = 400,
                TripleStrVar = false,
                ReopenAfter = true,
                MidSaveEvery = 0,
                VerifyEvery = 100,
            });
        }

        [TestMethod]
        public void Fuzz_Seed2002_900Steps_TripleVar_Reopen()
        {
            RunFuzzCore(new FuzzConfig
            {
                Seed = 2002,
                Steps = 900,
                IdRange = 500,
                TripleStrVar = true,
                ReopenAfter = true,
                MidSaveEvery = 0,
                VerifyEvery = 150,
            });
        }

        [TestMethod]
        public void Fuzz_Seed3003_1200Steps_HeavyChurn_Reopen()
        {
            RunFuzzCore(new FuzzConfig
            {
                Seed = 3003,
                Steps = 1200,
                IdRange = 600,
                TripleStrVar = false,
                ReopenAfter = true,
                MidSaveEvery = 0,
                VerifyEvery = 200,
            });
        }

        [TestMethod]
        public void Fuzz_Seed4004_800Steps_MidSaveBatches_Reopen()
        {
            RunFuzzCore(new FuzzConfig
            {
                Seed = 4004,
                Steps = 800,
                IdRange = 450,
                TripleStrVar = true,
                ReopenAfter = true,
                MidSaveEvery = 200,
                VerifyEvery = 0,
            });
        }

        [TestMethod]
        public void Fuzz_Seed5005_1500Steps_WideIdRange_Reopen()
        {
            RunFuzzCore(new FuzzConfig
            {
                Seed = 5005,
                Steps = 1500,
                IdRange = 1200,
                TripleStrVar = false,
                ReopenAfter = true,
                MidSaveEvery = 0,
                VerifyEvery = 250,
            });
        }

        [TestMethod]
        public void Fuzz_Seed6006_400Steps_DeleteRewriteWave_Reopen()
        {
            // Deterministic wave: fill → mass delete → rewrite same ids with opposite size tiers
            var path = Configuration.GetRandomPath();
            const int seed = 6006;
            var rng = new Random(seed);
            var expected = new Dictionary<int, RowExpect>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts, tripleStrVar: true);

                for (int wave = 0; wave < 4; wave++)
                {
                    int baseId = wave * 200;
                    for (int i = 0; i < 80; i++)
                    {
                        int id = baseId + i;
                        bool longish = i % 3 != 1;
                        var tier = longish ? SizeTier.Long : SizeTier.Tiny;
                        int sl = TierLength(rng, tier);
                        int bl = TierLength(rng, longish ? SizeTier.Medium : SizeTier.Short);
                        var row = BuildRow(id, sl, bl, seed);
                        Assert.IsTrue(TryInsert(ts, id, row, tripleStrVar: true), $"wave{wave} insert {id}");
                        expected[id] = row;
                    }

                    for (int i = 0; i < 80; i++)
                    {
                        if (i % 2 == 0)
                        {
                            int id = baseId + i;
                            Assert.IsTrue(ts.Delete(Table, "id", id).IsSuccess);
                            expected.Remove(id);
                        }
                    }

                    for (int i = 0; i < 80; i++)
                    {
                        int id = baseId + i;
                        if (expected.ContainsKey(id)) continue;
                        bool longish = i % 4 == 0;
                        var tier = longish ? SizeTier.XLarge : SizeTier.Short;
                        int sl = TierLength(rng, tier);
                        int bl = TierLength(rng, longish ? SizeTier.Long : SizeTier.Tiny);
                        var row = BuildRow(id, sl, bl, seed + wave);
                        Assert.IsTrue(TryInsert(ts, id, row, tripleStrVar: true), $"wave{wave} rewrite {id}");
                        expected[id] = row;
                    }

                    AssertExpected(ts, expected, $"wave{wave}-end");
                }

                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertExpected(ts, expected, "reopen-wave");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void Fuzz_Seed7007_500Steps_AlternatingEditDelete_Reopen()
        {
            var path = Configuration.GetRandomPath();
            const int seed = 7007;
            var rng = new Random(seed);
            var expected = new Dictionary<int, RowExpect>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts, tripleStrVar: false);

                for (int i = 0; i < 150; i++)
                {
                    int id = i;
                    var row = BuildRow(id,
                        TierLength(rng, i % 2 == 0 ? SizeTier.Long : SizeTier.Tiny),
                        TierLength(rng, i % 3 == 0 ? SizeTier.Medium : SizeTier.Short),
                        seed);
                    Assert.IsTrue(TryInsert(ts, id, row, false));
                    expected[id] = row;
                }

                for (int round = 0; round < 500; round++)
                {
                    int id = round % 150;
                    switch (round % 5)
                    {
                        case 0:
                        case 1:
                            if (expected.TryGetValue(id, out var ru))
                            {
                                int sl = TierLength(rng, RandomTier(rng));
                                int bl = TierLength(rng, RandomTier(rng));
                                ru.Body = MakeStr(id, sl, seed + round);
                                ru.Bin = MakeBin(id, bl, seed + round);
                                ru.StrLen = sl;
                                ru.BinLen = bl;
                                if (ts.Update(Table, "id", id, "body", ru.Body).IsSuccess &&
                                    ts.Update(Table, "id", id, "bin", ru.Bin).IsSuccess)
                                    expected[id] = ru;
                            }
                            break;
                        case 2:
                            if (expected.ContainsKey(id) && ts.Delete(Table, "id", id).IsSuccess)
                                expected.Remove(id);
                            break;
                        case 3:
                        case 4:
                            if (!expected.ContainsKey(id))
                            {
                                var row = BuildRow(id,
                                    TierLength(rng, RandomTier(rng)),
                                    TierLength(rng, RandomTier(rng)),
                                    seed + round);
                                if (TryInsert(ts, id, row, false))
                                    expected[id] = row;
                            }
                            break;
                    }

                    if ((round + 1) % 100 == 0)
                        AssertExpected(ts, expected, $"round{round + 1}");
                }

                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertExpected(ts, expected, "reopen-alt");
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
