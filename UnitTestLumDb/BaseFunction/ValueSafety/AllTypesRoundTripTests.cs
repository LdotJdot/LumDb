using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// H6: Mixed column types in one row survive close/reopen.
    /// </summary>
    [TestClass]
    public class AllTypesRoundTripTests
    {
        [TestMethod]
        public void H6_AllScalarAndVarTypes_RoundTripAfterReopen()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            var utc = new DateTime(2022, 2, 2, 2, 2, 2, DateTimeKind.Utc);
            byte[] bytes8 = [1, 2, 3, 4, 5];
            string longNote = new string('Z', 3000);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [
                    ("b", DbValueType.Bool, false),
                    ("i", DbValueType.Int, true),
                    ("u", DbValueType.UInt, false),
                    ("l", DbValueType.Long, false),
                    ("f", DbValueType.Float, false),
                    ("d", DbValueType.Double, false),
                    ("dec", DbValueType.Decimal, false),
                    ("dt", DbValueType.DateTimeUTC, false),
                    ("s8", DbValueType.Str8B, false),
                    ("s32", DbValueType.Str32B, false),
                    ("bin", DbValueType.Bytes8, false),
                    ("sv", DbValueType.StrVar, false),
                ]);

                var ins = ts.Insert(table, [
                    ("b", true),
                    ("i", 42),
                    ("u", 7u),
                    ("l", 99L),
                    ("f", 1.5f),
                    ("d", 2.5d),
                    ("dec", 1.2300m),
                    ("dt", utc),
                    ("s8", "hi"),
                    ("s32", "hello-world"),
                    ("bin", bytes8),
                    ("sv", longNote),
                ]);
                Assert.IsTrue(ins.IsSuccess, ins.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "i", 42);
                Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
                var v = found.Value;
                Assert.AreEqual(true, (bool)v[0]);
                Assert.AreEqual(42, (int)v[1]);
                Assert.AreEqual(7u, (uint)v[2]);
                Assert.AreEqual(99L, (long)v[3]);
                Assert.AreEqual(1.5f, (float)v[4]);
                Assert.AreEqual(2.5d, (double)v[5]);
                Assert.AreEqual(0, decimal.Compare(1.2300m, (decimal)v[6]));
                var dt = (DateTime)v[7];
                Assert.AreEqual(utc.Ticks, dt.Ticks);
                Assert.AreEqual(DateTimeKind.Utc, dt.Kind, "H6/H2: DateTime Kind");
                Assert.AreEqual("hi", (string)v[8]);
                Assert.AreEqual("hello-world", (string)v[9]);
                CollectionAssert.AreEqual(bytes8, ((byte[])v[10]).Take(bytes8.Length).ToArray());
                Assert.AreEqual(longNote, (string)v[11]);
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
