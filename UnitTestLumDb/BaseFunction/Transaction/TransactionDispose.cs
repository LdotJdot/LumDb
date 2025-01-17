using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class TransactionDispose
    {
        [TestMethod]
        public void TransactionDisposeAfterDbEngineDispose()
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
                    eng.SetDestoryOnDisposed();
                    eng.Dispose();
                }
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith(LumExceptionMessage.DbEngEarlyDisposed));
            }
        } 
        
     
    }
}