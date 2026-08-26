using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    [TestClass]
    public class DbCellApiTests
    {
        [TestMethod]
        public void InsertFind_DbCell_And_RowAccessors()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var utc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [
                    ("uid", DbValueType.Int, true),
                    ("name", DbValueType.Str32B, false),
                    ("when", DbValueType.DateTimeUTC, false),
                ]);
                var ins = ts.Insert(table, [
                    ("uid", 42),
                    ("name", "alice"),
                    ("when", utc),
                ]);
                Assert.IsTrue(ins.IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "uid", 42);
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual(42, found.Row.GetInt(0));
                Assert.AreEqual("alice", found.Row.GetString(1));
                Assert.AreEqual(utc, found.Row.GetDateTimeUtc(2));
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void GoThrough_RowView_CountsRows()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("uid", DbValueType.Int, true)]);
                for (int i = 0; i < 50; i++)
                    ts.Insert(table, [("uid", i)]);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                int count = 0;
                long sum = 0;
                ts.GoThrough(table, (ref RowView row) =>
                {
                    sum += row.GetInt(0);
                    count++;
                    return true;
                });
                Assert.AreEqual(50, count);
                Assert.AreEqual(50 * 49 / 2, sum);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void GoThrough_RowView_ReadsMixedScalarsWithoutMaterialize()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var utc = new DateTime(2021, 5, 5, 5, 5, 5, DateTimeKind.Utc);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [
                    ("uid", DbValueType.Int, true),
                    ("pay", DbValueType.Decimal, false),
                    ("when", DbValueType.DateTimeUTC, false),
                    ("name", DbValueType.Str32B, false),
                ]);
                ts.Insert(table, [
                    ("uid", 7),
                    ("pay", 1.2300m),
                    ("when", utc),
                    ("name", "bob"),
                ]);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                int hits = 0;
                ts.GoThrough(table, (ref RowView row) =>
                {
                    Assert.AreEqual(7, row.GetInt(0));
                    Assert.AreEqual(0, decimal.Compare(1.2300m, row.GetDecimal(1)));
                    Assert.AreEqual(utc, row.GetDateTimeUtc(2));
                    Assert.AreEqual("bob", row.GetString(3));
                    hits++;
                    return true;
                });
                Assert.AreEqual(1, hits);
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
