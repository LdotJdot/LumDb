using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class TransactionBlockException
    {
        [TestMethod]
        public void MultiTransactionInOneLocalThread()
        {
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();

                try
                {
                    using var ts2 = eng.StartTransaction();
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(ex.Message == LumExceptionMessage.IllegaTransaction);
                }

             
                eng.SetDestoryOnDisposed();
            }
        } 
        
        [TestMethod]
        public void TransactionInTransactionWithReadOnlyBehavior()
        {
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                string tb1 = "projAuthority";
                string tb2 = "projAuthority2";

                ts.Create(tb1, [("a", DbValueType.Int, true)]);
                ts.Create(tb2, [("a", DbValueType.Int, true)]);

                for (int i = 0; i < 10; i++)
                {
                    ts.Insert(tb1, [("a", i)]);
                }
                for (int i = 0; i < 5; i++)
                {
                    ts.Insert(tb2, [("a", i)]);
                }

                var rr = ts.Find(tb2, o => o).Values;

                var res = ts.Find(tb1, o => o.Where(
                    v => ts.Find(tb2, o => o.Where(k => (int)k[0] >= 0)).Values.Select(p => p[0]).Contains(v[0])
                    ));

                Assert.IsTrue(res.Values.Count() == 5);

                eng.SetDestoryOnDisposed();

            }
        }

        [TestMethod]
        public void TransactionInTransactionWithReadOnlyBehaviorMeetException()
        {
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                string tb1 = "projAuthority";
                string tb2 = "projAuthority2";

                ts.Create(tb1, [("a", DbValueType.Int, true)]);
                ts.Create(tb2, [("a", DbValueType.Int, true)]);

                for (int i = 0; i < 10; i++)
                {
                    ts.Insert(tb1, [("a", i)]);
                }
                for (int i = 0; i < 5; i++)
                {
                    ts.Insert(tb2, [("a", i)]);
                }

                var rr = ts.Find(tb2, o => o).Values;
                try
                {

                    var res = ts.Find(tb1, o => o.Where(
                        v => ts.Find(tb2, o => o.Where(k => (int)k[2] >= 0)).Values.Select(p => p[0]).Contains(v[0])
                        ));

                    Assert.IsTrue(res.Values.Count() == 5);
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(ex.Message == "Index was outside the bounds of the array.");
                }

                eng.SetDestoryOnDisposed();

            }
        }
    }
}