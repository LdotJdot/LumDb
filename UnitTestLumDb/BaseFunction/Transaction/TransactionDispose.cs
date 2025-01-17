using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using System.Diagnostics;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class TransactionDispose
    {
        [TestMethod]
        public void DbEngineDisposeTimeout()
        {
            var path = Configuration.GetRandomPath();
            try
            {

                using (DbEngine eng = Configuration.GetDbEngineForTest(path))
                {
                    Task.Run(() =>
                    {
                        var ts = eng.StartTransaction();
                        Thread.Sleep(10000);
                    });
                    Thread.Sleep(100);
                    eng.DisposeMillisecondsTimeout = 500;
                    eng.SetDestoryOnDisposed();
                    eng.Dispose();
                }
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.DbEngDisposedTimeOut));
            }
        }


        [TestMethod]
        public void TransactionDisposeAfterDbEngineDispose()
        {
            var path = Configuration.GetRandomPath();


            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                Task.Run(() =>
                {
                    try
                    {
                        using var ts = eng.StartTransaction();
                        Thread.Sleep(500);
                        Assert.Fail();
                    }
                    catch (Exception ex)
                    {
                        Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.DbEngDisposedEarly));
                    }
                });
                eng.SetDestoryOnDisposed();
                Thread.Sleep(100);

                eng.Dispose();
            }
            Thread.Sleep(700);


        }

    }


}