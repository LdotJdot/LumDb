using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class TransactionLock
    {
        [TestMethod]
        public void ReadWriteUpgradeLockParallel()
        {
            var path = Configuration.GetRandomPath();

            try
            {

                using (DbEngine eng = Configuration.GetDbEngineForTest(path))
                {
                    eng.TimeoutMilliseconds = 10000;


                    const string TABLENAME = "tableFirst";


                    eng.TimeoutMilliseconds = 10000; // set timeout to 10 seconds

                    using (var ts = eng.StartTransaction(0, false))
                    {
                        var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
                    }


                    using (ITransaction ts = eng.StartTransaction())
                    {
                        for (int i = 0; i < 1000; i++)
                        {
                            var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                        }
                    }


                    var t1 = Task.Run(() =>
                    {

                        using ITransaction ts = eng.StartTransaction();
                        Console.WriteLine("t1 started");
                        Task.Delay(1000).Wait();

                        var res = ts.Find(TABLENAME, 1);
                        Console.WriteLine("t1" + res.Value[0]);
                        ts.SaveChanges();
                        Console.WriteLine("t1 saves complete");
                        Task.Delay(5000).Wait();
                        Console.WriteLine("t1complete");
                    });

                    var t2 = Task.Run(() =>
                    {
                        Task.Delay(500).Wait();

                        using var ts = eng.StartTransactionReadonly();
                        Console.WriteLine("t2 started");
                        Task.Delay(3000).Wait();

                        var res = ts.Find(TABLENAME, 2);
                        Console.WriteLine("t2" + res.Value[0]);
                        Task.Delay(1000).Wait();
                        Console.WriteLine("t2complete");
                    });

                    Assert.IsTrue(Task.WaitAll([t1, t2], 10000));


                    eng.SetDestoryOnDisposed();
                }
            }
            catch(Exception ex)
            {
                Assert.Fail(ex.Message);
                throw;
            }

        }


        [TestMethod]
        public void WriteAfterReadInOneThreadEnvironment()
        {
            var path = Configuration.GetRandomPath();
            const string TABLENAME = "tableFirst";

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))

            using (var tsR = eng.StartTransactionReadonly())
            {

                Console.WriteLine("ts started");

                try
                {

                    using (ITransaction tsW = eng.StartTransaction())
                    {

                        Console.WriteLine("tsW started");

                        var resW = tsW.Find(TABLENAME, 1);
                        tsW.SaveChanges();
                        Console.WriteLine("tsW complete");
                    }


                    var res = tsR.Find(TABLENAME, 2);

                }
                catch (Exception ex)
                {
                    Assert.AreEqual(ex.Message, LumExceptionMessage.IllegaTransaction);
                }
            }
        }
        
        [TestMethod]
        public void ReadAfterWriteInOneThreadEnvironment()
        {
            var path = Configuration.GetRandomPath();
            const string TABLENAME = "tableFirst";

            using DbEngine eng = Configuration.GetDbEngineForTest(path);

            using (var tsWrite = eng.StartTransaction())
            {
                tsWrite.SaveChanges();

                using (var tsRead = eng.StartTransactionReadonly())
                {
                    var resW = tsRead.Find(TABLENAME, 1);                   
                }

                tsWrite.SaveChanges();

                var res = tsWrite.Find(TABLENAME, 2);

            }

        }

        [TestMethod]
        public void ReadAfterReadInOneThreadEnvironment()
        {
            var path = Configuration.GetRandomPath();
            const string TABLENAME = "tableFirst";

            using DbEngine eng = Configuration.GetDbEngineForTest(path);

            using (var tsWrite = eng.StartTransactionReadonly())
            {
                using (var tsRead = eng.StartTransactionReadonly())
                {
                    var resW = tsRead.Find(TABLENAME, 1);
                }

                var res = tsWrite.Find(TABLENAME, 2);

            }

        }


    }
}