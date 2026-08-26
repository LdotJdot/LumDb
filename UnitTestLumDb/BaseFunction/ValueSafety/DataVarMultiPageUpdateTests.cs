using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// H5: Multi-page StrVar/BytesVar update may copy wrong offset and corrupt data.
    /// </summary>
    [TestClass]
    public class DataVarMultiPageUpdateTests
    {
        private static string MakeLongString(int charCount)
        {
            return new string('A', charCount);
        }

        [TestMethod]
        public void H5_StrVar_UpdateLonger_RoundTripExact()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            // Large enough to span multiple var pages in typical page layouts
            string initial = MakeLongString(4000);
            string longer = MakeLongString(12000);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                var ins = ts.Insert(table, [("id", 1), ("body", initial)]);
                Assert.IsTrue(ins.IsSuccess, ins.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var upd = ts.Update(table, "id", 1, "body", longer);
                Assert.IsTrue(upd.IsSuccess, "update longer: " + upd.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "id", 1);
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual(longer, (string)found.Value[1],
                    "H5 RED: StrVar update to longer multi-page content corrupted.");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void H5_StrVar_UpdateShorter_StillMultiPage_RoundTripExact()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            string initial = MakeLongString(12000);
            string shorter = MakeLongString(5000);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("id", DbValueType.Int, true), ("body", DbValueType.StrVar, false)]);
                Assert.IsTrue(ts.Insert(table, [("id", 1), ("body", initial)]).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Update(table, "id", 1, "body", shorter).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "id", 1);
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual(shorter, (string)found.Value[1],
                    "H5 RED: StrVar update to shorter (still multi-page) corrupted.");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void H5_BytesVar_UpdateLonger_RoundTripExact()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            byte[] initial = Encoding.UTF8.GetBytes(MakeLongString(4000));
            byte[] longer = Encoding.UTF8.GetBytes(MakeLongString(12000));

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("id", DbValueType.Int, true), ("bin", DbValueType.BytesVar, false)]);
                Assert.IsTrue(ts.Insert(table, [("id", 1), ("bin", initial)]).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Update(table, "id", 1, "bin", longer).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "id", 1);
                Assert.IsTrue(found.IsSuccess);
                CollectionAssert.AreEqual(longer, (byte[])found.Value[1],
                    "H5 RED: BytesVar multi-page update corrupted.");
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
