using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// H8: After a multi-page StrVar is shortened in-place, NextPageId must be cleared.
    /// Continuation nodes are unlinked only (not deleted) when shared pages may hold other rows.
    /// </summary>
    [TestClass]
    public class DataVarTruncateUnlinkTests
    {
        [TestMethod]
        public void H8_StrVar_TruncateInPlace_WithSharedContinuationPage_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            // Large enough that, after many short vars fill the available page, the long
            // value spans pages with first-chunk SpaceLength still able to hold the shorter update.
            string longBody = new string('X', 4000);
            string shorter = new string('Y', 3000);

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [
                    ("id", DbValueType.Int, true),
                    ("body", DbValueType.StrVar, false),
                    ("bin", DbValueType.BytesVar, false),
                ]);

                for (int i = 0; i < 80; i++)
                {
                    Assert.IsTrue(ts.Insert(table, [
                        ("id", i),
                        ("body", i == 40 ? longBody : "s" + i),
                        ("bin", new byte[] { (byte)i }),
                    ]).IsSuccess);
                }

                // More rows after the long value so the continuation page gains other nodes.
                for (int i = 80; i < 160; i++)
                {
                    Assert.IsTrue(ts.Insert(table, [
                        ("id", i),
                        ("body", "s" + i),
                        ("bin", new byte[] { (byte)(i % 256) }),
                    ]).IsSuccess);
                }

                Assert.IsTrue(ts.Update(table, "id", 40, "body", shorter).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                var found = ts.Find(table, "id", 40);
                Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
                Assert.AreEqual(shorter, found.Row.GetString(1),
                    "H8 RED: truncated multi-page StrVar left NextPageId pointing at deleted continuation.");
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
