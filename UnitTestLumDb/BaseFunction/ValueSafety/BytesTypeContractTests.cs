using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Bytes columns accept byte[] only (not List&lt;byte&gt;).
    /// </summary>
    [TestClass]
    public class BytesTypeContractTests
    {
        [TestMethod]
        public void Bytes8_AcceptsByteArray_RejectsListByte()
        {
            var cell = DbCell.FromBytes(new byte[] { 1, 2, 3 }, DbValueType.Bytes8);
            Assert.IsTrue(DbValueType.Bytes8.CheckType(in cell));

            try
            {
                _ = DbCell.FromObject(new List<byte> { 1, 2, 3 });
                Assert.Fail("List<byte> must not become DbCell");
            }
            catch (Exception)
            {
                // expected
            }

            var path = Configuration.GetRandomPath();
            const string table = "t";

            using var eng = Configuration.GetDbEngineForTest(path);
            using var ts = eng.StartTransaction();
            ts.Create(table, [("bin", DbValueType.Bytes8, false)]);

            var withArray = ts.Insert(table, [("bin", new byte[] { 1, 2, 3 })]);
            Assert.IsTrue(withArray.IsSuccess, withArray.Exception?.Message);

            var found = ts.Find(table, 1u);
            Assert.IsTrue(found.IsSuccess);
            var got = found.Row.GetBytes(0);
            Assert.AreEqual(8, got.Length);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, got.AsSpan(0, 3).ToArray());
            eng.SetDestoryOnDisposed();
        }

        [TestMethod]
        public void BytesVar_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            const string table = "tv";
            var payload = new byte[4000];
            Random.Shared.NextBytes(payload);

            using (var eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                ts.Create(table, [("bin", DbValueType.BytesVar, false)]);
                Assert.IsTrue(ts.Insert(table, [("bin", payload)]).IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                var row = ts.Find(table, 1u);
                Assert.IsTrue(row.IsSuccess);
                CollectionAssert.AreEqual(payload, row.Row.GetBytes(0));
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
