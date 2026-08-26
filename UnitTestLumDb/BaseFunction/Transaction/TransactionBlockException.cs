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
                    ts.Insert(tb1, [("a", i)]);
                for (int i = 0; i < 5; i++)
                    ts.Insert(tb2, [("a", i)]);

                var set2 = new HashSet<int>();
                ts.GoThrough(tb2, (ref RowView r) =>
                {
                    set2.Add(r.GetInt(0));
                    return true;
                });

                int match = 0;
                ts.GoThrough(tb1, (ref RowView r) =>
                {
                    if (set2.Contains(r.GetInt(0)))
                        match++;
                    return true;
                });

                Assert.AreEqual(5, match);
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
                    ts.Insert(tb1, [("a", i)]);
                for (int i = 0; i < 5; i++)
                    ts.Insert(tb2, [("a", i)]);

                try
                {
                    ts.GoThrough(tb2, (ref RowView r) =>
                    {
                        _ = r.GetInt(2); // out of range — should throw
                        return true;
                    });
                    Assert.Fail("expected exception");
                }
                catch (Exception)
                {
                    // expected
                }

                eng.SetDestoryOnDisposed();
            }
        }
    }
}
