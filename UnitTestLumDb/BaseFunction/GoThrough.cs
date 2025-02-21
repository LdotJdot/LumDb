using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Value;
using System.Diagnostics;
using System.IO;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class GoThrough
    {
        [TestMethod]
        public void ThroughObject()
        {
            const string TABLENAME = "tableFirst";

            var path = Configuration.GetRandomPath();
            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using var ts = eng.StartTransaction();

                var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {                
                using ITransaction ts = eng.StartTransaction();

                for (int i = 0; i < 800; i++)
                {
                    var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                }
            }



            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {

                int count = 0;

                {
                    using ITransaction ts = eng.StartTransaction();

                    ts.GoThrough(TABLENAME, (object[] objs) =>
                    {
                        Console.WriteLine(objs[2].ToString());
                        count++;
                        if (count > 500) return false;
                        return true;
                    });
                }

                Assert.IsTrue(count == 501);
                eng.SetDestoryOnDisposed();

            }
        }
        
        [TestMethod]
        public void ThroughEntity()
        {
            const string TABLENAME = "tableFirst";

            var path = Configuration.GetRandomPath();
            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();

                ts.Create(TABLENAME, [("uid", DbValueType.Int, false), ("username", DbValueType.Str32B, true)]);

                for (int i = 0; i < 800; i++)
                {
                    ts.Insert(TABLENAME, [("username", "luojin"+i), ("uid", i+100)]);
                }
            }

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {

                int count = 0;

                {
                    using ITransaction ts = eng.StartTransaction();

                    var res = ts.Find(TABLENAME,1);


                    ts.GoThrough<Test>(TABLENAME, (Test objs) =>
                    {
                        Console.WriteLine(objs.uid);
                        count++;
                        if (count > 500) return false;
                        return true;
                    });
                }

                Assert.IsTrue(count == 501);
                eng.SetDestoryOnDisposed();

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

 }
