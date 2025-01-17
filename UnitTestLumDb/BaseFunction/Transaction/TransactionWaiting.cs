using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class TransactionWaiting
    {
        [TestMethod]
        public void MultiTransactionInOneLocalThread()
        {
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();

                var t = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using var ts2 = eng.StartTransaction(millisecondsTimeout:500);
                    }
                    catch (Exception ex)
                    {
                        Assert.IsTrue(ex.Message ==LumExceptionMessage.TransactionTimeout);
                    }
                });

                Thread.Sleep(1000);
                eng.SetDestoryOnDisposed();
            }
        } 
        
     
    }
}