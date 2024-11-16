using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class CreateTable
    {
        [TestMethod]
        public void CreateMultiplePage()
        {
            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();

                for (int i = 0; i < 100; i++)
                {
                    ts.Create("table" + i, [("uid" + i, DbValueType.Str32B, false), ("username" + i, DbValueType.Int, false)]);
                }
            }

            using (DbEngine eng2 = Configuration.GetDbEngineForTest(path))
            {
                {
                    using var ts = eng2.StartTransaction();
                    List<string> res = new List<string>(100);
                    for (int i = 0; i < 100; i++)
                    {
                    }
                }
                eng2.Destory();
            }
        }
    }
}