using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Regression tests for large StrVar/BytesVar update/insert (previously stackalloc'd full payload on stack).
    /// </summary>
    [TestClass]
    public partial class UpdateLargeVarStackOverflowTests
    {
        [LumEntity]
        public partial class StateRow
        {
            [Id]
            public uint Id { get; set; }

            [Key]
            public int SessionId { get; set; }

            public string Payload { get; set; } = "";
        }

        private static string MakePayload(int byteCount)
        {
            // UTF-8: one ASCII char == one byte
            return new string('P', byteCount);
        }

        /// <summary>
        /// Mirrors OpenLum AgentLumStore.UpsertState: insert small state, then UpdateEntity with large JSON-like payload.
        /// </summary>
        [TestMethod]
        public void UpdateEntity_LargeStrVar_ShouldRoundTrip_NotStackOverflow()
        {
            const string table = "agent_state";
            var path = Configuration.GetRandomPath();
            const int largeByteCount = 1536 * 1024;
            var largePayload = MakePayload(largeByteCount);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<StateRow>(table).IsSuccess);
                Assert.IsTrue(ts.Insert(table, new StateRow
                {
                    SessionId = 1,
                    Payload = "small",
                }).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.FindEntity<StateRow>(table, "SessionId", 1);
                Assert.IsTrue(found.IsSuccess);

                var update = ts.UpdateEntity(table, found.Value.Id, new StateRow
                {
                    SessionId = 1,
                    Payload = largePayload,
                });
                Assert.IsTrue(update.IsSuccess, update.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.FindEntity<StateRow>(table, "SessionId", 1);
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual(largeByteCount, found.Value.Payload.Length);
                Assert.AreEqual(largePayload, found.Value.Payload);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void UpdateColumn_LargeBytesVar_ShouldRoundTrip_NotStackOverflow()
        {
            const string table = "bin_state";
            var path = Configuration.GetRandomPath();
            const int largeByteCount = 1536 * 1024;
            var largePayload = Encoding.UTF8.GetBytes(MakePayload(largeByteCount));

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("id", DbValueType.Int, true), ("body", DbValueType.BytesVar, false)]);
                Assert.IsTrue(ts.Insert(table, [("id", 1), ("body", new byte[] { 1, 2, 3 })]).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var update = ts.Update(table, "id", 1, "body", largePayload);
                Assert.IsTrue(update.IsSuccess, update.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "id", 1);
                Assert.IsTrue(found.IsSuccess);
                CollectionAssert.AreEqual(largePayload, (byte[])found.Value[1]);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void UpdateEntity_GrowShrinkGrow_FindRoundTrip()
        {
            const string table = "agent_state";
            var path = Configuration.GetRandomPath();
            var sizes = new[] { 64, 80_000, 400_000, 1_536 * 1024, 12_000, 900_000 };

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<StateRow>(table).IsSuccess);
                Assert.IsTrue(ts.Insert(table, new StateRow { SessionId = 1, Payload = "small" }).IsSuccess);
                ts.SaveChanges();
            }

            string last = "small";
            foreach (var size in sizes)
            {
                last = MakePayload(size);
                using var eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();
                var found = ts.FindEntity<StateRow>(table, "SessionId", 1);
                Assert.IsTrue(found.IsSuccess, $"find before size={size}: {found.Exception?.Message}");
                var update = ts.UpdateEntity(table, found.Value.Id, new StateRow { SessionId = 1, Payload = last });
                Assert.IsTrue(update.IsSuccess, $"update size={size}: {update.Exception?.Message}");
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.FindEntity<StateRow>(table, "SessionId", 1);
                Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
                Assert.AreEqual(last, found.Value.Payload);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void InsertEntity_LargeStrVar_ShouldRoundTrip()
        {
            const string table = "agent_state";
            var path = Configuration.GetRandomPath();
            const int largeByteCount = 1536 * 1024;
            var largePayload = MakePayload(largeByteCount);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<StateRow>(table).IsSuccess);
                Assert.IsTrue(ts.Insert(table, new StateRow
                {
                    SessionId = 42,
                    Payload = largePayload,
                }).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.FindEntity<StateRow>(table, "SessionId", 42);
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual(largeByteCount, found.Value.Payload.Length);
                Assert.AreEqual(largePayload, found.Value.Payload);
                eng.SetDestoryOnDisposed();
            }
        }

        /// <summary>
        /// Simulates deep async/grpc stack frames before UpdateEntity.
        /// </summary>
        [TestMethod]
        public void UpdateEntity_LargeStrVar_AfterDeepStackUse_ShouldRoundTrip_NotStackOverflow()
        {
            const int payloadBytes = 384 * 1024;
            var path = Configuration.GetRandomPath();
            var payload = MakePayload(payloadBytes);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<StateRow>("agent_state").IsSuccess);
                Assert.IsTrue(ts.Insert("agent_state", new StateRow { SessionId = 1, Payload = "small" }).IsSuccess);
                ts.SaveChanges();
            }

            Exception? updateError = null;
            var completed = false;

            try
            {
                // ~96 × 8 KB ≈ 768 KB stack retained at leaf (simulates deep gRPC/async frames).
                RunWithStackPressure(depth: 96, frameBytes: 8 * 1024, () =>
                {
                    using var eng = Configuration.GetDbEngineForTest(path);
                    using var ts = eng.StartTransaction();
                    var row = ts.FindEntity<StateRow>("agent_state", "SessionId", 1);
                    Assert.IsTrue(row.IsSuccess);
                    var update = ts.UpdateEntity("agent_state", row.Value.Id, new StateRow
                    {
                        SessionId = 1,
                        Payload = payload,
                    });
                    Assert.IsTrue(update.IsSuccess, update.Exception?.Message);
                    ts.SaveChanges();
                    completed = true;
                });
            }
            catch (Exception ex)
            {
                updateError = ex;
            }

            Assert.IsNull(updateError, updateError?.ToString());
            Assert.IsTrue(completed,
                "UpdateEntity with ~384 KB StrVar should succeed after deep stack use.");

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var row = ts.FindEntity<StateRow>("agent_state", "SessionId", 1);
                Assert.IsTrue(row.IsSuccess);
                Assert.AreEqual(payload, row.Value.Payload);
                eng.SetDestoryOnDisposed();
            }
        }

        private static void RunWithStackPressure(int depth, int frameBytes, Action action)
        {
            if (depth <= 0)
            {
                action();
                return;
            }

            Span<byte> frame = stackalloc byte[frameBytes];
            frame[0] = (byte)depth;
            RunWithStackPressure(depth - 1, frameBytes, action);
        }
    }
}
