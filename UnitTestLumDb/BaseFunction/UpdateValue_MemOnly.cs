using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class UpdateValue_MemOnly
    {
        [TestMethod]
        public void UpdateByKeyValue_InMem()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

            for (int i = 0; i < 500; i++)
            {
                ts.Insert(TABLENAME, new TestVar() { uid = i * 100, username = "anonymous" + (i + 2), content = (i * 50).ToString() });
            }

            var fd = ts.Find<TestVar>(TABLENAME, "username", "anonymous2");
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 0);
            Assert.IsTrue(fd.Value.username == "anonymous2");
            Assert.IsTrue(fd.Value.content == "0");
            string str1 = "dsadaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var res0 = ts.Update(TABLENAME,
                o => o.username == "anonymous2"
                , new TestVar() { uid = 2999, username = "kkl", content = str1 });

            fd = ts.Find<TestVar>(TABLENAME, "uid", 0);
            Assert.IsTrue(!fd.IsSuccess);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 2999);

            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 2999);
            Assert.IsTrue(fd.Value.username == "kkl");
            Assert.IsTrue(fd.Value.content == str1);
            // to longer var
            var res = ts.Update(TABLENAME, o => o.uid == 200, new TestVar() { uid = 3001, username = "kkl2", content = str1 });
            fd = ts.Find<TestVar>(TABLENAME, "uid", 200);
            Assert.IsTrue(fd.IsSuccess == false);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl2");
            Assert.IsTrue(fd.Value.content == str1);

            // to longer var over page;
            string str2 = "dsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var res2 = ts.Update(TABLENAME, o => o.uid == 3001,
                new TestVar() { uid = 3001, username = "kkl3", content = str2 }
               );
            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl3");
            Assert.IsTrue(fd.Value.content == str2);

            // to shorter var with page clean;
            string str3 = "new shorter string";
            var res3 = ts.Update(TABLENAME, o => o.uid == 3001, new TestVar() { uid = 3001, username = "kkl9", content = str3 });
            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl9");
            Assert.IsTrue(fd.Value.content == str3);

            // add new strVar without new page request;
            int d = 500;
            string str4 = (d * 50).ToString();

            // todo hash collision loop
            ts.Insert(TABLENAME, new TestVar() { uid = d * 100, username = "anonymous" + (d + 2), content = str4 });
            fd = ts.Find<TestVar>(TABLENAME, "uid", 50000);
            Assert.IsTrue(fd.Value.id == 501);
            Assert.IsTrue(fd.Value.uid == 50000);
            Assert.IsTrue(fd.Value.username == "anonymous502");
            Assert.IsTrue(fd.Value.content == str4);
        }

        [TestMethod]
        public void UpdateById()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

            for (int i = 0; i < 500; i++)
            {
                ts.Insert(TABLENAME, new TestVar() { uid = i * 100, username = "anonymous" + (i + 2), content = (i * 50).ToString() });
            }

            var fd = ts.Find<TestVar>(TABLENAME, "username", "anonymous2");
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 0);
            Assert.IsTrue(fd.Value.username == "anonymous2");
            Assert.IsTrue(fd.Value.content == "0");
            string str1 = "dsadaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var res0 = ts.Update(TABLENAME,
                1
                , new TestVar() { uid = 2999, username = "kkl", content = str1 });

            fd = ts.Find<TestVar>(TABLENAME, "uid", 0);
            Assert.IsTrue(!fd.IsSuccess);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 2999);

            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 2999);
            Assert.IsTrue(fd.Value.username == "kkl");
            Assert.IsTrue(fd.Value.content == str1);
            // to longer var
            var res = ts.Update(TABLENAME, 3, new TestVar() { uid = 3001, username = "kkl2", content = str1 });
            fd = ts.Find<TestVar>(TABLENAME, "uid", 200);
            Assert.IsTrue(fd.IsSuccess == false);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl2");
            Assert.IsTrue(fd.Value.content == str1);

            // to longer var over page;
            string str2 = "dsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaadsa1111111111111111122222222222222222222222222222222222222222222222222233333333333333333333333333333333333344444444444444444444455555555555555555555555555555555666666666666666666666666666666666666666666666777777777777777777777777777777777888888888888888888888888888888899999999999999999999999999999daaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var res2 = ts.Update(TABLENAME, 3,
                new TestVar() { uid = 3001, username = "kkl3", content = str2 }
               );
            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl3");
            Assert.IsTrue(fd.Value.content == str2);

            // to shorter var with page clean;
            string str3 = "new shorter string";
            var res3 = ts.Update(TABLENAME, o => o.uid == 3001, new TestVar() { uid = 3001, username = "kkl9", content = str3 });
            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl9");
            Assert.IsTrue(fd.Value.content == str3);

            // add new strVar without new page request;
            int d = 500;
            string str4 = (d * 50).ToString();
            ts.Insert(TABLENAME, new TestVar() { uid = d * 100, username = "anonymous" + (d + 2), content = str4 });
            fd = ts.Find<TestVar>(TABLENAME, "uid", 50000);
            Assert.IsTrue(fd.Value.id == 501);
            Assert.IsTrue(fd.Value.uid == 50000);
            Assert.IsTrue(fd.Value.username == "anonymous502");
            Assert.IsTrue(fd.Value.content == str4);
        }

        [TestMethod]
        public void UpdateByIdColumnNameKey()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

            for (int i = 0; i < 500; i++)
            {
                ts.Insert(TABLENAME, new TestVar() { uid = i * 100, username = "anonymous" + (i + 2), content = (i * 50).ToString() });
            }

            var fd = ts.Find<TestVar>(TABLENAME, "username", "anonymous2");
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 0);
            Assert.IsTrue(fd.Value.username == "anonymous2");
            Assert.IsTrue(fd.Value.content == "0");
            string str1 = "dsadaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            ts.Update(TABLENAME, 1, "uid", 2999);
            ts.Update(TABLENAME, 1, "username", "kkl");
            ts.Update(TABLENAME, 1, "content", str1);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 0);
            Assert.IsTrue(!fd.IsSuccess);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 2999);
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 2999);
            Assert.IsTrue(fd.Value.username == "kkl");
            Assert.IsTrue(fd.Value.content == str1);
            // to longer var

            ts.Update(TABLENAME, 3, "uid", 3001);
            ts.Update(TABLENAME, 3, "username", "kkl2");
            ts.Update(TABLENAME, 3, "content", str1);
            fd = ts.Find<TestVar>(TABLENAME, "uid", 200);
            Assert.IsTrue(fd.IsSuccess == false);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl2");
            Assert.IsTrue(fd.Value.content == str1);
        }

        [TestMethod]
        public void UpdateByColumnNameKeyT()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

            for (int i = 0; i < 500; i++)
            {
                ts.Insert(TABLENAME, new TestVar() { uid = i * 100, username = "anonymous" + (i + 2), content = (i * 50).ToString() });
            }

            var fd = ts.Find<TestVar>(TABLENAME, "username", "anonymous2");

            // sometime bug find
            Console.WriteLine(fd.Exception?.Message);
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 0);
            Assert.IsTrue(fd.Value.username == "anonymous2");
            Assert.IsTrue(fd.Value.content == "0");
            Console.WriteLine(fd.Value.ToString());
            string str1 = "dsadaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            ts.Update(TABLENAME, "username", "anonymous2", new TestVar() { uid = 2999, username = "kkl", content = str1 });

            fd = ts.Find<TestVar>(TABLENAME, "uid", 0);
            Assert.IsTrue(!fd.IsSuccess);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 2999);
            Console.WriteLine("2999: " + fd.Value.ToString());
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 2999);
            Assert.IsTrue(fd.Value.username == "kkl");
            Assert.IsTrue(fd.Value.content == str1);
            // to longer var

            ts.Update(TABLENAME, "uid", 200, new TestVar() { uid = 3001, username = "kkl2", content = str1 });

            fd = ts.Find<TestVar>(TABLENAME, "uid", 200);
            Assert.IsTrue(fd.IsSuccess == false);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Console.WriteLine("200: " + fd.Value.ToString());
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl2");
            Assert.IsTrue(fd.Value.content == str1);
        }

        [TestMethod]
        public void UpdateByColumnNameKeyColumnNameKey()
        {
            const string TABLENAME = "tableFirst";
            using DbEngine eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

            for (int i = 0; i < 500; i++)
            {
                ts.Insert(TABLENAME, new TestVar() { uid = i * 100, username = "anonymous" + (i + 2), content = (i * 50).ToString() });
            }

            var fd = ts.Find<TestVar>(TABLENAME, "username", "anonymous2");
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 0);
            Assert.IsTrue(fd.Value.username == "anonymous2");
            Assert.IsTrue(fd.Value.content == "0");
            Console.WriteLine(fd.Value.ToString());
            string str1 = "dsadaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            ts.Update(TABLENAME, "uid", 0, "uid", 2999);
            ts.Update(TABLENAME, "uid", 2999, "username", "kkl");
            ts.Update(TABLENAME, "uid", 2999, "content", str1);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 0);
            Assert.IsTrue(!fd.IsSuccess);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 2999);
            Console.WriteLine("2999: " + fd.Value.ToString());
            Assert.IsTrue(fd.Value.id == 1);
            Assert.IsTrue(fd.Value.uid == 2999);
            Assert.IsTrue(fd.Value.username == "kkl");
            Assert.IsTrue(fd.Value.content == str1);
            // to longer var

            ts.Update(TABLENAME, "uid", 200, "uid", 3001);
            ts.Update(TABLENAME, "uid", 3001, "username", "kkl2");
            ts.Update(TABLENAME, "uid", 3001, "content", str1);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 200);
            Assert.IsTrue(fd.IsSuccess == false);

            fd = ts.Find<TestVar>(TABLENAME, "uid", 3001);
            Console.WriteLine("200: " + fd.Value.ToString());
            Assert.IsTrue(fd.Value.id == 3);
            Assert.IsTrue(fd.Value.uid == 3001);
            Assert.IsTrue(fd.Value.username == "kkl2");
            Assert.IsTrue(fd.Value.content == str1);
        }

        public class TestVar : IDbEntity
        {
            public uint id;
            public string username;
            public string content;
            public int uid;

            IDbEntity IDbEntity.Unboxing(object[] obj)
            {
                uid = (int)obj[0];
                username = (string)obj[1];
                content = (string)obj[2];
                return this;
            }

            object[] IDbEntity.Boxing()
            {
                return [uid, username, content];
            }

            void IDbEntity.GetId(uint id)
            {
                this.id = id;
            }

            public override string ToString()
            {
                return $@"""id"":{id}, ""uid"":{uid}, ""username"":{username},""content"":{content}";
            }
        }
    }
}