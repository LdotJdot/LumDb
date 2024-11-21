using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using System.Diagnostics;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class InsertDataAndSelectData
    {
        [TestMethod]
        public void CreatePage()
        {
            var path = Configuration.GetRandomPath();

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();
                ts.Create("tableFirst", [("uid", DbValueType.Int, false), ("username", DbValueType.Str32B, false)]);
                ts.Create("tableSecond", [("uid", DbValueType.Int, false), ("username", DbValueType.Str32B, false)]);

                for (int i = 0; i < 1000; i++)
                {
                    ts.Insert("tableFirst", new Test() { uid = i * 100, username = "anonymous" + (i + 2) });
                }

                Debug.WriteLine("create done");
            }

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();
                // obj
                var dbResultObj = ts.Find("tableFirst", o => o.Where(l => (int)l[0] < 500));
                int id = 0;
                Assert.IsTrue(dbResultObj.Values.Count == 5);
                foreach (var v in dbResultObj.Values)
                {
                    Assert.IsTrue((int)v[0] == id * 100);
                    Assert.IsTrue((string)v[1] == "anonymous" + (id + 2));
                    id++;
                }

                var dbResult = ts.Find<Test2>("tableFirst", o => o.Where(e => e.username2.EndsWith("5")).Take(1));

                foreach (var v in dbResult.Values)
                {
                    Assert.IsTrue(v.username2 == "anonymous5");
                    Assert.IsTrue(v.uid == 300);
                }
            }
        }

        [TestMethod]
        public void InsertValueWithDifferentOrder()
        {
            var path = Configuration.GetRandomPath();

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();

                ts.Create("tableFirst", [("uid", DbValueType.Int, false), ("username", DbValueType.Str32B, true)]);

                ts.Insert("tableFirst", [("username", "luojin"), ("uid", 223)]);
            }

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();

                var res = ts.Find<Test>("tableFirst", "username", "luojin");
                ;
            }
        }
    }

    public class Test2 : IDbEntity
    {
        public string username2;
        public int uid;

        public IDbEntity Unboxing(object[] obj)
        {
            username2 = (string)obj[1];
            uid = (int)obj[0];
            return this;
        }

        public object[] Boxing()
        {
            return [uid, username2];
        }
    }

    public class Test : IDbEntity
    {
        public string username;
        public uint id;
        public int uid;

        public IDbEntity Unboxing(object[] obj)
        {
            username = (string)obj[1];
            uid = (int)obj[0];
            return this;
        }

        public object[] Boxing()
        {
            return [uid, username];
        }

        public void GetId(uint id)
        {
            this.id = id;
        }

        public override string ToString()
        {
            return $@"""id"":{id}, ""uid"":{uid}, ""username"":{username}";
        }
    }
}