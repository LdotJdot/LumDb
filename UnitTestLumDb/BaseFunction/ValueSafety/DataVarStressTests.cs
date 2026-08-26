using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Stress tests for StrVar/BytesVar under fragmentation: long+short interleaving,
    /// deletes, updates, gap reuse, and disk reopen.
    /// </summary>
    [TestClass]
    public class DataVarStressTests
    {
        private const string Table = "var_stress";

        private static string LongStr(int id, int len = 5000) => new string((char)('A' + (id % 26)), len) + id;
        private static byte[] LongBytes(int id, int len = 4000) => Encoding.UTF8.GetBytes(LongStr(id, len));
        private static string ShortStr(int id) => "s" + id;
        private static byte[] ShortBytes(int id) => [(byte)(id & 0xFF), (byte)((id >> 8) & 0xFF)];

        private static (string body, byte[] bin) Payload(int id, bool isLong)
            => isLong ? (LongStr(id), LongBytes(id)) : (ShortStr(id), ShortBytes(id));

        private static void CreateTable(ITransaction ts)
        {
            Assert.IsTrue(ts.Create(Table, [
                ("id", DbValueType.Int, true),
                ("body", DbValueType.StrVar, false),
                ("bin", DbValueType.BytesVar, false),
            ]).IsSuccess);
        }

        private static void InsertRow(ITransaction ts, int id, bool isLong)
        {
            var (body, bin) = Payload(id, isLong);
            Assert.IsTrue(ts.Insert(Table, [("id", id), ("body", body), ("bin", bin)]).IsSuccess,
                $"insert id={id} long={isLong}");
        }

        private static void AssertRow(ITransaction ts, int id, bool expectLong, string? phase = null)
        {
            var found = ts.Find(Table, "id", id);
            var tag = phase == null ? $"id={id}" : $"{phase} id={id}";
            Assert.IsTrue(found.IsSuccess, tag + ": " + found.Exception?.Message);

            var (body, bin) = Payload(id, expectLong);
            Assert.AreEqual(body, found.Row.GetString(1), tag + " body");
            CollectionAssert.AreEqual(bin, found.Row.GetBytes(2), tag + " bin");
        }

        private static void AssertAllRows(ITransaction ts, IReadOnlyDictionary<int, bool> expected, string phase)
        {
            foreach (var (id, isLong) in expected)
                AssertRow(ts, id, isLong, phase);

            int scanned = 0;
            ts.GoThrough(Table, (ref RowView row) =>
            {
                scanned++;
                return true;
            });
            Assert.AreEqual(expected.Count, scanned, $"{phase}: row count mismatch");
        }

        [TestMethod]
        public void V1_InterleavedLongShort_Write_Reopen_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, bool>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);
                for (int i = 0; i < 120; i++)
                {
                    bool isLong = i % 2 == 0;
                    expected[i] = isLong;
                    InsertRow(ts, i, isLong);
                }
                AssertAllRows(ts, expected, "same-session");
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertAllRows(ts, expected, "reopen");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void V2_InterleavedDelete_Survivors_Reopen()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, bool>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);
                for (int i = 0; i < 100; i++)
                {
                    bool isLong = i % 3 != 1;
                    InsertRow(ts, i, isLong);
                    if (i % 2 == 0)
                        expected[i] = isLong;
                }

                for (int i = 1; i < 100; i += 2)
                    Assert.IsTrue(ts.Delete(Table, "id", i).IsSuccess, $"delete id={i}");

                AssertAllRows(ts, expected, "after-delete");
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertAllRows(ts, expected, "reopen-after-delete");
                for (int i = 1; i < 100; i += 2)
                    Assert.IsFalse(ts.Find(Table, "id", i).IsSuccess, $"deleted id={i} should be gone");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void V3_GapFormation_LongThenShort_DeleteLong_Rewrite_Reopen()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, bool>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);

                // Phase A: many long vals — allocate multi-page chains
                for (int i = 0; i < 40; i++)
                {
                    InsertRow(ts, i, isLong: true);
                    expected[i] = true;
                }

                // Phase B: short vals fill remaining page space (gap-like layout)
                for (int i = 1000; i < 1200; i++)
                {
                    InsertRow(ts, i, isLong: false);
                    expected[i] = false;
                }

                // Phase C: delete every other long row — free var nodes, leave holes
                for (int i = 0; i < 40; i += 2)
                {
                    Assert.IsTrue(ts.Delete(Table, "id", i).IsSuccess);
                    expected.Remove(i);
                }

                // Phase D: rewrite into freed slots + new ids with mixed sizes
                for (int i = 0; i < 40; i += 2)
                {
                    bool isLong = i % 4 == 0;
                    InsertRow(ts, i, isLong);
                    expected[i] = isLong;
                }
                for (int i = 2000; i < 2050; i++)
                {
                    bool isLong = i % 5 == 0;
                    InsertRow(ts, i, isLong);
                    expected[i] = isLong;
                }

                AssertAllRows(ts, expected, "after-gap-rewrite");
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertAllRows(ts, expected, "reopen-gap");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void V4_InterleavedUpdate_LongShortSwap_Reopen()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, bool>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);
                for (int i = 0; i < 80; i++)
                {
                    bool isLong = i % 2 == 0;
                    InsertRow(ts, i, isLong);
                    expected[i] = isLong;
                }

                // Swap sizes: long->short, short->long, in interleaved order
                for (int i = 0; i < 80; i++)
                {
                    bool newLong = i % 2 != 0;
                    var (body, bin) = Payload(i, newLong);
                    Assert.IsTrue(ts.Update(Table, "id", i, "body", body).IsSuccess, $"upd body {i}");
                    Assert.IsTrue(ts.Update(Table, "id", i, "bin", bin).IsSuccess, $"upd bin {i}");
                    expected[i] = newLong;
                }

                AssertAllRows(ts, expected, "after-swap");
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertAllRows(ts, expected, "reopen-swap");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void V5_MassiveVarPages_TruncateInPlace_ThenMoreWrites_Reopen()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, bool>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);

                for (int i = 0; i < 60; i++)
                    InsertRow(ts, i, isLong: true);

                for (int i = 60; i < 260; i++)
                    InsertRow(ts, i, isLong: false);

                // In-place truncate (H8 scenario): long rows shortened while pages hold other nodes
                for (int i = 0; i < 60; i += 3)
                {
                    Assert.IsTrue(ts.Update(Table, "id", i, "body", ShortStr(i)).IsSuccess);
                    Assert.IsTrue(ts.Update(Table, "id", i, "bin", ShortBytes(i)).IsSuccess);
                }

                for (int i = 260; i < 360; i++)
                    InsertRow(ts, i, isLong: i % 7 == 0);

                for (int i = 0; i < 360; i++)
                {
                    bool isLong;
                    if (i < 60 && i % 3 == 0)
                        isLong = false;
                    else if (i < 60)
                        isLong = true;
                    else if (i < 260)
                        isLong = false;
                    else
                        isLong = i % 7 == 0;
                    expected[i] = isLong;
                }

                AssertAllRows(ts, expected, "after-truncate-wave");
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertAllRows(ts, expected, "reopen-truncate");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void V6_DeleteShortKeepLong_ThenRewriteShort_Reopen()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, bool>();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);

                for (int round = 0; round < 3; round++)
                {
                    int baseId = round * 500;
                    for (int i = 0; i < 50; i++)
                    {
                        int id = baseId + i;
                        bool isLong = i % 4 == 0;
                        InsertRow(ts, id, isLong);
                        expected[id] = isLong;
                    }

                    // delete all short rows in this round
                    for (int i = 0; i < 50; i++)
                    {
                        int id = baseId + i;
                        if (!expected[id]) // short
                        {
                            Assert.IsTrue(ts.Delete(Table, "id", id).IsSuccess);
                            expected.Remove(id);
                        }
                    }

                    // rewrite short into deleted slots
                    for (int i = 0; i < 50; i++)
                    {
                        int id = baseId + i;
                        if (!expected.ContainsKey(id))
                        {
                            bool isLong = i % 6 == 0;
                            InsertRow(ts, id, isLong);
                            expected[id] = isLong;
                        }
                    }
                }

                AssertAllRows(ts, expected, "after-rounds");
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                AssertAllRows(ts, expected, "reopen-rounds");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void V7_FullScan_HashAfterChurn_Reopen()
        {
            var path = Configuration.GetRandomPath();
            var expected = new Dictionary<int, (string body, byte[] bin)>();
            var rng = new Random(42);

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                CreateTable(ts);

                for (int step = 0; step < 8; step++)
                {
                    int id = step * 17 + 3;
                    bool isLong = step % 2 == 0;
                    var (body, bin) = Payload(id, isLong);
                    InsertRow(ts, id, isLong);
                    expected[id] = (body, bin);
                }

                for (int churn = 0; churn < 30; churn++)
                {
                    int op = churn % 4;
                    int id = rng.Next(0, 200);
                    switch (op)
                    {
                        case 0: // insert if absent
                            if (!expected.ContainsKey(id))
                            {
                                bool isLong = id % 3 == 0;
                                var p = Payload(id, isLong);
                                if (ts.Insert(Table, [("id", id), ("body", p.body), ("bin", p.bin)]).IsSuccess)
                                    expected[id] = p;
                            }
                            break;
                        case 1: // delete if present
                            if (expected.ContainsKey(id))
                            {
                                if (ts.Delete(Table, "id", id).IsSuccess)
                                    expected.Remove(id);
                            }
                            break;
                        case 2: // update to long
                            if (expected.ContainsKey(id))
                            {
                                var p = Payload(id, isLong: true);
                                if (ts.Update(Table, "id", id, "body", p.body).IsSuccess &&
                                    ts.Update(Table, "id", id, "bin", p.bin).IsSuccess)
                                    expected[id] = p;
                            }
                            break;
                        case 3: // update to short
                            if (expected.ContainsKey(id))
                            {
                                var p = Payload(id, isLong: false);
                                if (ts.Update(Table, "id", id, "body", p.body).IsSuccess &&
                                    ts.Update(Table, "id", id, "bin", p.bin).IsSuccess)
                                    expected[id] = p;
                            }
                            break;
                    }
                }

                foreach (var (id, (body, bin)) in expected)
                {
                    var found = ts.Find(Table, "id", id);
                    Assert.IsTrue(found.IsSuccess, $"churn id={id}");
                    Assert.AreEqual(body, found.Row.GetString(1));
                    CollectionAssert.AreEqual(bin, found.Row.GetBytes(2));
                }

                int count = 0;
                ts.GoThrough(Table, (ref RowView row) => { count++; return true; });
                Assert.AreEqual(expected.Count, count);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                foreach (var (id, (body, bin)) in expected)
                {
                    var found = ts.Find(Table, "id", id);
                    Assert.IsTrue(found.IsSuccess, $"reopen churn id={id}");
                    Assert.AreEqual(body, found.Row.GetString(1));
                    CollectionAssert.AreEqual(bin, found.Row.GetBytes(2));
                }
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
