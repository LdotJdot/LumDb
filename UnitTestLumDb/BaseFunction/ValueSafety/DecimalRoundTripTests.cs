using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// H3: Decimal round-trip including scale and extremes.
    /// </summary>
    [TestClass]
    public class DecimalRoundTripTests
    {
        [TestMethod]
        public void H3_Decimal_ScaleAndExtremes_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            const string table = "t";
            decimal[] values =
            [
                0m,
                1m,
                -1m,
                1.2300m,
                decimal.MinValue,
                decimal.MaxValue,
                0.0000000000000000001m,
            ];

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [("id", DbValueType.Int, true), ("pay", DbValueType.Decimal, true)]);
                for (int i = 0; i < values.Length; i++)
                {
                    var ins = ts.Insert(table, [("id", i), ("pay", values[i])]);
                    Assert.IsTrue(ins.IsSuccess, $"insert {values[i]}: " + ins.Exception?.Message);
                }
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                for (int i = 0; i < values.Length; i++)
                {
                    var byId = ts.Find(table, "id", i);
                    Assert.IsTrue(byId.IsSuccess);
                    Assert.AreEqual(0, decimal.Compare(values[i], (decimal)byId.Value[1]),
                        $"H3 RED: id={i} expected {values[i]} got {byId.Value[1]}");

                    var byPay = ts.Find(table, "pay", values[i]);
                    Assert.IsTrue(byPay.IsSuccess,
                        $"H3 RED: Find by decimal key failed for {values[i]}. " + byPay.Exception?.Message);
                    Assert.AreEqual(i, (int)byPay.Value[0]);
                }
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
