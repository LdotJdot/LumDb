using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class DeleteData
    {
        [TestMethod]
        public void InfiniteLoopInHashCollision()
        {
            var path = Configuration.GetRandomPath();

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);

                using var ts = eng.StartTransaction();

                ts.Create("tableFirst", [("uid", DbValueType.Int, true), ("username", DbValueType.Str8B, true)]);

                for (int i = 0; i < 4; i++)
                {
                    ts.Insert("tableFirst", new Test() { uid = -i + 5, username = (-i).ToString() });
                    ts.Insert("tableFirst", new Test() { uid = -i + 5000, username = (-i + 5000).ToString() });
                    var res = ts.Delete("tableFirst", (uint)i + 1);
                }
            }

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);

                using var ts = eng.StartTransaction();
                var xx0 = ts.Find("tableFirst", 3);
                Assert.IsFalse(xx0.IsSuccess);

                var xx1 = ts.Find("tableFirst", 5);
                Assert.AreEqual(xx1.Value[0], 3);
                Assert.AreEqual(xx1.Value[1], "-2");
                var xx2 = ts.Find("tableFirst", "uid", 4997);
                Assert.AreEqual(xx2.Value[0], 4997);
                Assert.AreEqual(xx2.Value[1], "4997");
                var xx3 = ts.Find("tableFirst", "username", "4998");
                Assert.AreEqual(xx3.Value[0], 4998);
                Assert.AreEqual(xx3.Value[1], "4998");
                Console.WriteLine("done");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void Delete()
        {
            DbEngine eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();

            ts.Create("tableFirst", [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true)]);

            for (int i = 0; i < 1000; i++)
            {
                ts.Insert("tableFirst", new Test() { uid = -i + 5, username = (-i).ToString() });
                ts.Insert("tableFirst", new Test() { uid = -i + 5000, username = (-i + 5000).ToString() });
                //ts.Insert("tableFirst", new Test() { uid = (i + 1) * 100, username = (i+1).ToString() });
                var res = ts.Delete("tableFirst", (uint)i + 1);

                if (i > 5)
                {
                    // LogInfo(db);
                    ;
                }
            }

            var repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "37");
            var resC = ts.Find("tableFirst", o => o);
            Assert.AreEqual(resC.Values.Count, 1000);

            var xx = ts.Find("tableFirst", 1004);
            Assert.AreEqual(xx.Value[0], 4499);
            Assert.AreEqual(xx.Value[1], "4499");
            var xx2 = ts.Find("tableFirst", "username", "4500");
            Assert.AreEqual(xx2.Value[0], 4500);
            Assert.AreEqual(xx2.Value[1], "4500");
            var xx3 = ts.Find("tableFirst", "uid", -899);
            Assert.AreEqual(xx3.Value[0], -899);
            Assert.AreEqual(xx3.Value[1], "-904");
            ;
        }
    }
}