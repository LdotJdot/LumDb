﻿using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class DropTable_MemOnly
    {
        [TestMethod]
        public void DropTableAfterDeleteAndShouldNotChangePageSize()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();

            using var ts = eng.StartTransaction();

            ts.Create("tableFirst", [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true)]);

            for (int i = 0; i < 1000; i++)
            {
                ts.Insert("tableFirst", new Test() { uid = -i + 5, username = (-i).ToString() });
                ts.Insert("tableFirst", new Test() { uid = -i + 5000, username = (-i + 5000).ToString() });
                var res = ts.Delete("tableFirst", (uint)i + 1);
            }

            var repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "37");
            var resC = ts.Find("tableFirst", o => o);
            Assert.AreEqual(resC.Values.Count, 1000);

            var xx = ts.Find("tableFirst", 1004);
            Assert.AreEqual(xx.Value[0], 4499);
            Assert.AreEqual(xx.Value[1], "4499");
            var xx2 = ts.Find("tableFirst", "username", "4500");
            Assert.AreEqual(xx2.Value[0], 4500);
            Assert.AreEqual(xx2.Value[1], "4500");
            var xx3 = ts.Find("tableFirst", "uid", -899);
            Assert.AreEqual(xx3.Value[0], -899);
            Assert.AreEqual(xx3.Value[1], "-904");
            ;
            ts.Drop(TABLENAME);

            var totalPage = ((LumTransaction)ts).DbState().Split("\r\n")[1].Split(' ')[1];
            Assert.AreEqual(totalPage, "94");

            repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "37");
            ;
            ts.Create("tableSecond", [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true)]);

            for (int i = 0; i < 1000; i++)
            {
                ts.Insert("tableSecond", new Test() { uid = -i + 5, username = (-i - 10).ToString() });
                ts.Insert("tableSecond", new Test() { uid = -i + 5000, username = (-i + 5001).ToString() });
                var res = ts.Delete("tableSecond", (uint)i + 1);
            }

            repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "37");
            resC = ts.Find("tableSecond", o => o);
            Assert.AreEqual(resC.Values.Count, 1000);

            xx = ts.Find("tableSecond", 1004);
            Assert.AreEqual(xx.Value[0], 4499);
            Assert.AreEqual(xx.Value[1], "4500");
            xx2 = ts.Find("tableSecond", "username", "4501");
            Assert.AreEqual(xx2.Value[0], 4500);
            Assert.AreEqual(xx2.Value[1], "4501");
            xx3 = ts.Find("tableSecond", "uid", -899);
            Assert.AreEqual(xx3.Value[0], -899);
            Assert.AreEqual(xx3.Value[1], "-914");

            totalPage = ((LumTransaction)ts).DbState().Split("\r\n")[1].Split(' ')[1];
            Assert.AreEqual(totalPage, "94");
            repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "37");
            ;
        }

        public void UpdateByColumnNameKeyTDataVar()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();

            using var ts = eng.StartTransaction();

            ts.Create("tableFirst", [("uid", DbValueType.Int, true), ("username", DbValueType.StrVar, false)]);

            for (int i = 0; i < 1000; i++)
            {
                ts.Insert("tableFirst", new Test() { uid = -i + 5, username = (-i).ToString() });
                ts.Insert("tableFirst", new Test() { uid = -i + 5000, username = (-i + 5000).ToString() });
                var res = ts.Delete("tableFirst", (uint)i + 1);
            }

            var repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "25");
            var resC = ts.Find("tableFirst", o => o);
            Assert.AreEqual(resC.Values.Count, 1000);

            var xx = ts.Find("tableFirst", 1004);
            Assert.AreEqual(xx.Value[0], 4499);
            Assert.AreEqual(xx.Value[1], "4499");
            var xx2 = ts.Find("tableFirst", "uid", 4500);
            Assert.AreEqual(xx2.Value[0], 4500);
            Assert.AreEqual(xx2.Value[1], "4500");
            var xx3 = ts.Find("tableFirst", "uid", -899);
            Assert.AreEqual(xx3.Value[0], -899);
            Assert.AreEqual(xx3.Value[1], "-904");
            ;
            ts.Drop(TABLENAME);
            ;
            ts.Create("tableSecond", [("uid", DbValueType.Int, true), ("username", DbValueType.StrVar, false)]);

            for (int i = 0; i < 1000; i++)
            {
                ts.Insert("tableSecond", new Test() { uid = -i + 5, username = (-i - 10).ToString() });
                ts.Insert("tableSecond", new Test() { uid = -i + 5000, username = (-i + 5001).ToString() });
                var res = ts.Delete("tableSecond", (uint)i + 1);
            }

            repoSize = ((LumTransaction)ts).DbState().Split("\r\n")[5].Split(' ')[1];
            Assert.AreEqual(repoSize, "25");
            resC = ts.Find("tableSecond", o => o);
            Assert.AreEqual(resC.Values.Count, 1000);

            xx = ts.Find("tableSecond", 1004);
            Assert.AreEqual(xx.Value[0], 4499);
            Assert.AreEqual(xx.Value[1], "4500");
            xx2 = ts.Find("tableSecond", "uid", 4500);
            Assert.AreEqual(xx2.Value[0], 4500);
            Assert.AreEqual(xx2.Value[1], "4501");
            xx3 = ts.Find("tableSecond", "uid", -899);
            Assert.AreEqual(xx3.Value[0], -899);
            Assert.AreEqual(xx3.Value[1], "-914");
            ;
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

            public void SetId(uint id)
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