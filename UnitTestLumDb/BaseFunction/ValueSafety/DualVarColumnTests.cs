using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    [TestClass]
    public class DualVarColumnTests
    {
        [TestMethod]
        public void H7_SameRow_StrVar_And_BytesVar_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var longStr = new string('Z', 4000);
            byte[] bytes = [1, 2, 3, 4, 5];

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [
                    ("id", DbValueType.Int, true),
                    ("s", DbValueType.StrVar, false),
                    ("b", DbValueType.BytesVar, false),
                ]);
                Assert.IsTrue(ts.Insert(table, [("id", 1), ("s", longStr), ("b", bytes)]).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                var found = ts.Find(table, "id", 1);
                Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
                Assert.AreEqual(longStr, found.Row.GetString(1));
                CollectionAssert.AreEqual(bytes, found.Row.GetBytes(2));
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void H7_ManyRows_OneLongStrVar_WithBytesVar_SameSession()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var longStr = new string('Z', 4000);

            using var eng = new DbEngine(path);
            using var ts = eng.StartTransaction();
            ts.Create(table, [
                ("id", DbValueType.Int, true),
                ("s", DbValueType.StrVar, false),
                ("b", DbValueType.BytesVar, false),
            ]);
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(ts.Insert(table, [
                    ("id", i),
                    ("s", i == 50 ? longStr : "s" + i),
                    ("b", new byte[] { (byte)i }),
                ]).IsSuccess);
            }

            var found = ts.Find(table, "id", 50);
            Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
            Assert.AreEqual(longStr, found.Row.GetString(1));
            eng.SetDestoryOnDisposed();
        }
    }
}
