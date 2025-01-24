using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
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

                var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {                
                using ITransaction ts = eng.StartTransaction();

                for (int i = 0; i < 50000; i++)
                {
                    var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                }
            }



            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {

                using ITransaction ts1 = eng.StartTransaction();

                var ds = ts1.Find(TABLENAME, o => (o.Where(l => ((long)l[1]) % 3 == 0)));
                var count = ds.Values.Count;

                var ds2 = ts1.Count(TABLENAME, [("b", (o) => (long)o % 3 == 0)]);
                var count2 = ds2.Value[0];

                Assert.IsTrue(count == (uint)count2);
                eng.SetDestoryOnDisposed();

            }
        }
    }

 }
