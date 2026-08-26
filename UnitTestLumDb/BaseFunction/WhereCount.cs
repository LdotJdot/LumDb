using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using System.Diagnostics;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class WhereCount
    {
        [TestMethod]
        public void CountData()
        {
            const string TABLENAME = "tableFirst";
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using ITransaction ts = eng.StartTransaction();
                for (int i = 0; i < 5000; i++)
                {
                    ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                }
            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts1 = eng.StartTransaction();
                var count = RowScan.Count(ts1, TABLENAME, (ref RowView r) => r.GetLong(1) % 3 == 0);
                var ds2 = ts1.Count(TABLENAME, (ref RowView r) => r.GetLong(1) % 3 == 0);
                Assert.AreEqual((uint)count, ds2.Value);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void WhereMethod()
        {
            const string TABLENAME = "tableFirst";
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using (var ts = eng.StartTransaction(0, false))
                {
                    ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
                }
            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using ITransaction ts = eng.StartTransaction();
                for (int i = 0; i < 1000; i++)
                {
                    ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                }
            }

            int res1 = 0;
            int res2 = 0;
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                var rows = new List<int>();
                ts.GoThrough(TABLENAME, (ref RowView r) =>
                {
                    if (r.GetInt(0) % 3 == 0)
                        rows.Add(r.GetInt(0));
                    return true;
                });
                res1 = rows.Skip(5).Take(300).ElementAt(10);
            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                var rows = new List<int>();
                uint skipped = 0;
                ts.GoThrough(TABLENAME, (ref RowView r) =>
                {
                    if (r.GetInt(0) % 3 != 0)
                        return true;
                    if (skipped < 5)
                    {
                        skipped++;
                        return true;
                    }
                    if (rows.Count >= 300)
                        return false;
                    rows.Add(r.GetInt(0));
                    return true;
                });
                res2 = rows[10];
            }

            Assert.AreEqual(res1, res2);
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
