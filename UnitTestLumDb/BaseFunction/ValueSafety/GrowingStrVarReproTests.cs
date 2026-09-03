using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Narrow repro: Insert 8MB works; Update 4MB→8MB truncates after reopen (~4MB).
    /// </summary>
    [TestClass]
    public class GrowingStrVarReproTests
    {
        private static string P(int n, char c) => new string(c, n);

        [TestMethod]
        public void Insert_8MB_StrVar_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            var body = P(8 * 1024 * 1024, 'X');
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                Assert.IsTrue(ts.Insert("t", [("id", 1), ("body", body)]).IsSuccess);
                ts.SaveChanges();
            }
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var f = ts.Find("t", "id", 1);
                Assert.IsTrue(f.IsSuccess, f.Exception?.Message);
                Assert.AreEqual(body.Length, f.Row.GetString(1).Length);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void Update_4MB_to_8MB_SameSession_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            var body8 = P(8 * 1024 * 1024, 'B');
            using var eng = Configuration.GetDbEngineForTest(path);
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                Assert.IsTrue(ts.Insert("t", [("id", 1), ("body", P(4 * 1024 * 1024, 'A'))]).IsSuccess);
                Assert.IsTrue(ts.Update("t", "id", 1, "body", body8).IsSuccess);
                var f = ts.Find("t", "id", 1);
                Assert.IsTrue(f.IsSuccess, f.Exception?.Message);
                Assert.AreEqual(body8.Length, f.Row.GetString(1).Length, "same-session length");
            }
            eng.SetDestoryOnDisposed();
        }

        [TestMethod]
        public void Update_4MB_to_8MB_Save_ThenFindSameEngine_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            var body8 = P(8 * 1024 * 1024, 'B');
            using var eng = Configuration.GetDbEngineForTest(path);
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                Assert.IsTrue(ts.Insert("t", [("id", 1), ("body", P(4 * 1024 * 1024, 'A'))]).IsSuccess);
                ts.SaveChanges();
            }
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Update("t", "id", 1, "body", body8).IsSuccess);
                ts.SaveChanges();
            }
            using (var ts = eng.StartTransaction())
            {
                var f = ts.Find("t", "id", 1);
                Assert.IsTrue(f.IsSuccess, f.Exception?.Message);
                Assert.AreEqual(body8.Length, f.Row.GetString(1).Length, "same-engine after save length");
            }
            eng.SetDestoryOnDisposed();
        }

        [TestMethod]
        public void Update_1MB_to_2MB_Reopen_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                Assert.IsTrue(ts.Insert("t", [("id", 1), ("body", P(1 * 1024 * 1024, 'A'))]).IsSuccess);
                ts.SaveChanges();
            }
            var body2 = P(2 * 1024 * 1024, 'B');
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Update("t", "id", 1, "body", body2).IsSuccess);
                ts.SaveChanges();
            }
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var f = ts.Find("t", "id", 1);
                Assert.IsTrue(f.IsSuccess, f.Exception?.Message);
                Assert.AreEqual(body2.Length, f.Row.GetString(1).Length);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void Update_4MB_to_8MB_Reopen_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create("t", [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                Assert.IsTrue(ts.Insert("t", [("id", 1), ("body", P(4 * 1024 * 1024, 'A'))]).IsSuccess);
                ts.SaveChanges();
            }
            var body8 = P(8 * 1024 * 1024, 'B');
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Update("t", "id", 1, "body", body8).IsSuccess);
                ts.SaveChanges();
            }
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var f = ts.Find("t", "id", 1);
                Assert.IsTrue(f.IsSuccess, f.Exception?.Message);
                Assert.AreEqual(body8.Length, f.Row.GetString(1).Length, "4->8MB reopen length");
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
