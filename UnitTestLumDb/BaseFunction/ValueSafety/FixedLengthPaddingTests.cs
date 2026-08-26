using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// H1: Fixed-length fields may retain uninitialized tail bytes when value is shorter than capacity.
    /// </summary>
    [TestClass]
    public class FixedLengthPaddingTests
    {
        [TestMethod]
        public void H1_SerializeShortString_IntoDirtyBuffer_ReadBackMustEqualOriginal()
        {
            Span<byte> buffer = stackalloc byte[8];
            buffer.Fill(0xFF);

            "ab".SerializeObjectToBytes(buffer);

            var roundTrip = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
            Assert.AreEqual("ab", roundTrip,
                "H1 RED: short Str8B left non-zero garbage in unused tail (or TrimEnd hid non-zero junk).");
        }

        [TestMethod]
        public void H1_SerializeShortBytes_IntoDirtyBuffer_UnusedTailMustBeZero()
        {
            Span<byte> buffer = stackalloc byte[8];
            buffer.Fill(0xFF);

            new byte[] { 1, 2, 3 }.SerializeObjectToBytes(buffer);

            Assert.AreEqual((byte)1, buffer[0]);
            Assert.AreEqual((byte)2, buffer[1]);
            Assert.AreEqual((byte)3, buffer[2]);
            for (int i = 3; i < 8; i++)
            {
                Assert.AreEqual((byte)0, buffer[i],
                    $"H1 RED: Bytes8 unused byte at index {i} was 0x{buffer[i]:X2}, expected 0x00.");
            }
        }

        [TestMethod]
        public void H1_ShortStr8B_AsKey_FindMustHit()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("k", DbValueType.Str8B, true), ("v", DbValueType.Int, false)]);
                var ins = ts.Insert(table, [("k", "xy"), ("v", 7)]);
                Assert.IsTrue(ins.IsSuccess, ins.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "k", "xy");
                Assert.IsTrue(found.IsSuccess, "H1 RED: Find by short Str8B key failed. " + found.Exception?.Message);
                Assert.AreEqual("xy", (string)found.Value[0]);
                Assert.AreEqual(7, (int)found.Value[1]);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void H1_ShortenUpdate_Str32B_ReadBackExact()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("name", DbValueType.Str32B, true)]);
                ts.Insert(table, [("name", "abcdefgh")]);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var upd = ts.Update(table, "name", "abcdefgh", "name", "ab");
                Assert.IsTrue(upd.IsSuccess, upd.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find(table, "name", "ab");
                Assert.IsTrue(found.IsSuccess, "H1 RED: after shorten update, Find by new key failed. " + found.Exception?.Message);
                Assert.AreEqual("ab", (string)found.Value[0]);
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
