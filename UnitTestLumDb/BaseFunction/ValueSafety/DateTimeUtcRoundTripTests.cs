using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// H2: DateTimeUTC write uses Ticks but read uses FromBinary — Kind may be wrong.
    /// </summary>
    [TestClass]
    public class DateTimeUtcRoundTripTests
    {
        [TestMethod]
        public void H2_InsertUtc_ReadBack_KindMustBeUtc()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var utc = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("when", DbValueType.DateTimeUTC, false)]);
                var ins = ts.Insert(table, [("when", utc)]);
                Assert.IsTrue(ins.IsSuccess, ins.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, 1u);
                Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
                var got = (DateTime)found.Value[0];
                Assert.AreEqual(utc.Ticks, got.Ticks);
                Assert.AreEqual(DateTimeKind.Utc, got.Kind,
                    "H2 RED: DateTimeUTC round-trip lost Utc Kind (got " + got.Kind + ").");
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void H2_ReadThenReInsert_SameValueMustSucceed()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var utc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("when", DbValueType.DateTimeUTC, false)]);
                ts.Insert(table, [("when", utc)]);
                ts.SaveChanges();
            }

            DateTime readBack;
            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                readBack = (DateTime)ts.Find(table, 1u).Value[0];
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var ins = ts.Insert(table, [("when", readBack)]);
                Assert.IsTrue(ins.IsSuccess,
                    "H2 RED: value read from DB cannot be re-inserted. " + ins.Exception?.Message);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void H2_DeserializeBytes_Direct_KindMustBeUtc()
        {
            var utc = new DateTime(2023, 3, 3, 3, 3, 3, DateTimeKind.Utc);
            Span<byte> buffer = stackalloc byte[8];
            ((object)utc).SerializeObjectToBytes(buffer);

            var obj = buffer.DeserializeBytesToObject(DbValueType.DateTimeUTC);
            var got = (DateTime)obj;
            Assert.AreEqual(utc.Ticks, got.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, got.Kind,
                "H2 RED: DeserializeBytesToObject does not restore Utc Kind.");
        }
    }
}
