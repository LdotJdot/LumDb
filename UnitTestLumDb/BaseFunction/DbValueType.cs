using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class DbValueTypeData
    {
        [TestMethod]
        public void DecimalAndDateTime()
        {
            var path = Configuration.GetRandomPath();

            const string TABLENAME = "tableFirst";

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();
                ts.Create(TABLENAME, [
                    ("uid", DbValueType.Int, true),
                ("username", DbValueType.Str32B, true),
                ("dec", DbValueType.Decimal, true),
                ("time", DbValueType.DateTimeUTC, false),
                ]);

                for (int i = 0; i < 500; i++)
                {
                    ts.Insert(TABLENAME, new TestDecimalDateTime()
                    {
                        uid = i * 100,
                        username = "anonymous" + (i + 2),
                        time = DateTime.UtcNow,
                        dec = new decimal(3.141592653 + i)
                    });
                }
            }

            using (DbEngine eng2 = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng2.StartTransaction();
                var res = ts.Find<TestDecimalDateTime>(TABLENAME, "dec", new decimal(7.141592653));
                Assert.IsTrue(res.Value.id == 5);
                Assert.IsTrue(res.Value.uid == 400);
                Assert.IsTrue(res.Value.username == "anonymous6");
                Assert.IsTrue(decimal.Compare(res.Value.dec, new decimal(7.141592653)) == 0);
                eng2.Destory();
            }
        }

        public class TestDecimalDateTime : IDbEntity
        {
            public uint id;
            public int uid;
            public string username;
            public decimal dec;
            public DateTime time;

            IDbEntity IDbEntity.Unboxing(object[] obj)
            {
                uid = (int)obj[0];
                username = (string)obj[1];
                dec = (decimal)obj[2];
                time = (DateTime)obj[3];
                return this;
            }

            object[] IDbEntity.Boxing()
            {
                return [uid, username, dec, time];
            }

            void IDbEntity.GetId(uint id)
            {
                this.id = id;
            }

            public override string ToString()
            {
                return $@"""id"":{id}, ""uid"":{uid}, ""username"":{username},""dec"":{dec},""time"":{time.ToLocalTime()}";
            }
        }
    }
}