using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    [TestClass]
    public partial class LargeVarColumnFilterTests
    {
        [LumEntity]
        public partial class OrderDoc
        {
            [Id]
            public uint Id { get; set; }

            [Key]
            public int OrderNo { get; set; }

            public int Score { get; set; }

            public string Body { get; set; } = "";
        }

        [TestMethod]
        public void FilterOnScore_DoesNotTouchLargeBodyVar()
        {
            const string table = "orders";
            var path = Configuration.GetRandomPath();
            var big = new string('X', 80_000);

            using var eng = Configuration.GetDbEngineForTest(path);
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create<OrderDoc>(table).IsSuccess);
                for (int i = 0; i < 40; i++)
                {
                    var r = ts.Insert(table, new OrderDoc
                    {
                        OrderNo = 1000 + i,
                        Score = i % 7,
                        Body = big,
                    });
                    Assert.IsTrue(r.IsSuccess);
                }
                ts.SaveChanges();
            }

            using (var ts = eng.StartTransaction())
            {
                DataVarManager.GetDataVarCallCount = 0;
                var found = ts.Find<OrderDoc>(table, (ref RowView r) => OrderDoc.GetScore(ref r) == 3);
                Assert.IsTrue(found.IsSuccess);
                var hits = found.Values.Count();
                Assert.IsTrue(hits > 0);
                // Only matching rows are materialized (each loads Body once), not all 40 rows.
                Assert.AreEqual(hits, DataVarManager.GetDataVarCallCount);
                Assert.IsTrue(DataVarManager.GetDataVarCallCount < 40);
            }

            using (var ts = eng.StartTransaction())
            {
                DataVarManager.GetDataVarCallCount = 0;
                uint n = 0;
                ts.GoThrough(table, (ref RowView r) =>
                {
                    if (OrderDoc.GetScore(ref r) == 3)
                        n++;
                    return true;
                });
                Assert.IsTrue(n > 0);
                Assert.AreEqual(0, DataVarManager.GetDataVarCallCount);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void FindByKey_MaterializesOnlyOneRow()
        {
            const string table = "orders";
            var path = Configuration.GetRandomPath();
            var big = new string('Y', 40_000);

            using var eng = Configuration.GetDbEngineForTest(path);
            using (var ts = eng.StartTransaction())
            {
                ts.Create<OrderDoc>(table);
                for (int i = 0; i < 20; i++)
                {
                    ts.Insert(table, new OrderDoc { OrderNo = i, Score = i, Body = big });
                }
                ts.SaveChanges();
            }

            using (var ts = eng.StartTransaction())
            {
                DataVarManager.GetDataVarCallCount = 0;
                var one = ts.FindEntity<OrderDoc>(table, "OrderNo", 7);
                Assert.IsTrue(one.IsSuccess);
                Assert.AreEqual(7, one.Value.Score);
                Assert.AreEqual(big.Length, one.Value.Body.Length);
                Assert.AreEqual(1, DataVarManager.GetDataVarCallCount);
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
