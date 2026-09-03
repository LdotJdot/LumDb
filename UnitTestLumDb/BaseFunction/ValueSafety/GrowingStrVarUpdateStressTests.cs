using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Stress: one StrVar entity grows by repeated UpdateEntity up to 10 MB,
    /// while other StrVar rows are inserted and deleted in between.
    /// </summary>
    [TestClass]
    public partial class GrowingStrVarUpdateStressTests
    {
        [LumEntity]
        public partial class GrowRow
        {
            [Id]
            public uint Id { get; set; }

            [Key]
            public int Key { get; set; }

            public string Body { get; set; } = "";
        }

        private static string Payload(int size, char c) => new string(c, size);

        /// <summary>
        /// Grow primary row: 64KB → 256KB → 1MB → 2MB → 4MB → 8MB → 10MB.
        /// Between each grow step: insert short/medium noise rows, delete some, verify survivors.
        /// After each grow: FindEntity must round-trip exact body.
        /// </summary>
        [TestMethod]
        public void GrowingStrVar_UpdateEntity_To10MB_WithInterleavedNoise_RoundTrip()
        {
            const string table = "grow";
            var path = Configuration.GetRandomPath();
            int[] sizes =
            [
                64 * 1024,
                256 * 1024,
                1 * 1024 * 1024,
                2 * 1024 * 1024,
                4 * 1024 * 1024,
                8 * 1024 * 1024,
                10 * 1024 * 1024,
            ];

            var noiseAlive = new Dictionary<int, string>();
            int noiseId = 1000;
            string expectedBody = "seed";

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<GrowRow>(table).IsSuccess);
                Assert.IsTrue(ts.Insert(table, new GrowRow { Key = 1, Body = expectedBody }).IsSuccess);
                ts.SaveChanges();
            }

            for (int step = 0; step < sizes.Length; step++)
            {
                int size = sizes[step];
                expectedBody = Payload(size, (char)('A' + (step % 26)));

                // --- grow primary via UpdateEntity ---
                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    var found = ts.FindEntity<GrowRow>(table, "Key", 1);
                    Assert.IsTrue(found.IsSuccess, $"step={step} find before grow: {found.Exception?.Message}");

                    var upd = ts.UpdateEntity(table, found.Value.Id, new GrowRow { Key = 1, Body = expectedBody });
                    Assert.IsTrue(upd.IsSuccess, $"step={step} update size={size}: {upd.Exception?.Message}");
                    ts.SaveChanges();
                }

                // --- interleaved noise: insert short + medium StrVar ---
                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    for (int n = 0; n < 8; n++)
                    {
                        int id = noiseId++;
                        int noiseSize = (n % 2 == 0) ? 64 + (n * 17) : 8_000 + (n * 100);
                        var body = Payload(noiseSize, (char)('a' + (n % 26)));
                        Assert.IsTrue(ts.Insert(table, new GrowRow { Key = id, Body = body }).IsSuccess,
                            $"step={step} insert noise key={id}: failed");
                        noiseAlive[id] = body;
                    }

                    // delete half of previously alive noise (odd keys)
                    var toDelete = noiseAlive.Keys.Where(k => k % 2 == 1).Take(6).ToList();
                    foreach (var id in toDelete)
                    {
                        Assert.IsTrue(ts.Delete(table, "Key", id).IsSuccess, $"step={step} delete key={id}");
                        noiseAlive.Remove(id);
                    }

                    ts.SaveChanges();
                }

                // --- verify primary + remaining noise (reopen) ---
                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    var primary = ts.FindEntity<GrowRow>(table, "Key", 1);
                    Assert.IsTrue(primary.IsSuccess, $"step={step} primary find: {primary.Exception?.Message}");
                    Assert.AreEqual(size, primary.Value.Body.Length, $"step={step} primary length");
                    Assert.AreEqual(expectedBody, primary.Value.Body, $"step={step} primary content");

                    foreach (var (id, body) in noiseAlive)
                    {
                        var row = ts.FindEntity<GrowRow>(table, "Key", id);
                        Assert.IsTrue(row.IsSuccess, $"step={step} noise key={id}: {row.Exception?.Message}");
                        Assert.AreEqual(body, row.Value.Body, $"step={step} noise key={id} content");
                    }

                    if (step == sizes.Length - 1)
                        eng.SetDestoryOnDisposed();
                }
            }
        }

        /// <summary>
        /// Same grow path but also shrink mid-way then grow again past previous peak,
        /// with noise churn, to exercise in-place truncate + reallocate chains.
        /// </summary>
        [TestMethod]
        public void GrowingStrVar_GrowShrinkGrow_To10MB_WithNoise_RoundTrip()
        {
            const string table = "grow2";
            var path = Configuration.GetRandomPath();
            // grow → shrink → grow past peak → 10MB
            int[] sizes =
            [
                512 * 1024,
                3 * 1024 * 1024,
                128 * 1024,           // shrink
                5 * 1024 * 1024,
                10 * 1024 * 1024,
            ];

            var noiseAlive = new Dictionary<int, string>();
            int noiseId = 2000;
            string expected = "x";

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<GrowRow>(table).IsSuccess);
                Assert.IsTrue(ts.Insert(table, new GrowRow { Key = 1, Body = expected }).IsSuccess);
                // seed some noise before grow
                for (int i = 0; i < 20; i++)
                {
                    int id = noiseId++;
                    var body = Payload(200 + i * 50, 'n');
                    Assert.IsTrue(ts.Insert(table, new GrowRow { Key = id, Body = body }).IsSuccess);
                    noiseAlive[id] = body;
                }
                ts.SaveChanges();
            }

            for (int step = 0; step < sizes.Length; step++)
            {
                int size = sizes[step];
                expected = Payload(size, (char)('Z' - (step % 26)));

                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    var found = ts.FindEntity<GrowRow>(table, "Key", 1);
                    Assert.IsTrue(found.IsSuccess, $"step={step} find: {found.Exception?.Message}");
                    Assert.IsTrue(ts.UpdateEntity(table, found.Value.Id, new GrowRow { Key = 1, Body = expected }).IsSuccess,
                        $"step={step} update size={size}");

                    // churn: insert 5, delete 5
                    for (int n = 0; n < 5; n++)
                    {
                        int id = noiseId++;
                        var body = Payload(3_000 + n * 500, 'q');
                        Assert.IsTrue(ts.Insert(table, new GrowRow { Key = id, Body = body }).IsSuccess);
                        noiseAlive[id] = body;
                    }
                    foreach (var id in noiseAlive.Keys.Take(5).ToList())
                    {
                        Assert.IsTrue(ts.Delete(table, "Key", id).IsSuccess);
                        noiseAlive.Remove(id);
                    }

                    ts.SaveChanges();
                }

                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    var primary = ts.FindEntity<GrowRow>(table, "Key", 1);
                    Assert.IsTrue(primary.IsSuccess, $"step={step} verify: {primary.Exception?.Message}");
                    Assert.AreEqual(expected, primary.Value.Body, $"step={step} body");

                    foreach (var (id, body) in noiseAlive)
                    {
                        var row = ts.FindEntity<GrowRow>(table, "Key", id);
                        Assert.IsTrue(row.IsSuccess, $"step={step} noise {id}: {row.Exception?.Message}");
                        Assert.AreEqual(body, row.Value.Body);
                    }

                    if (step == sizes.Length - 1)
                        eng.SetDestoryOnDisposed();
                }
            }
        }
    }
}
