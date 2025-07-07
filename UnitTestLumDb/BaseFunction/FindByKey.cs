using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using System.Diagnostics;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class FindByKey
    {
        [TestMethod]
        public void FindWithMultipleKeyPos()
        {
            var path = Configuration.GetRandomPath();

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();
                ts.Create("tableFirst", [("uid", DbValueType.Int, false), ("username", DbValueType.Str32B, true)]);

                for (int i = 0; i < 500; i++)
                {
                    ts.Insert("tableFirst", new Test() { uid = i * 100, username = "anonymous" + (i + 2) });
                }
            }

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransactionReadonly();
                var idRes = ts.Find("tableFirst", 499);
                Assert.IsTrue(idRes.IsSuccess == true);
                Debug.Assert((int)idRes.Value[0] == 49800);
                Debug.Assert((string)idRes.Value[1] == "anonymous500");

                var dr = ts.Find<Test2>("tableFirst", o => o.Where(l => l.uid == 4990000));
                Assert.IsTrue(dr.Values.Count == 0);

                var res0 = ts.Find("tableFirst", "uid", "100");
                Debug.Assert(res0.IsSuccess == false);

                var res = ts.Find("tableFirst", "username", "anonymous499");
                Debug.Assert((string)res.Value[1] == "anonymous499");

                var tRes = ts.Find<Test>("tableFirst", "username", "anonymous360");
                Debug.Assert(tRes.Value.username == "anonymous360");
            }
        }
    }
}