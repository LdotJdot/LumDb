﻿using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class InsertLongStrVar
    {
        [TestMethod]
        public void InsertStrVar()
        {
            var path = Configuration.GetRandomPath();
            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();

                ts.Create("tableFirst", [("uid", DbValueType.Int, true), ("username", DbValueType.StrVar, false)]);
                ts.Insert("tableFirst", new Test() { uid = 4 /*i * 100*/, username = strVarLong });
                ts.Insert("tableFirst", new Test() { uid = 3 /*i * 100*/, username = "aa123" });
            }

            {
                using DbEngine eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();
                var res = ts.Find("tableFirst", 1);
                Assert.AreEqual(res.Value[1], strVarLong);
                var res2 = ts.Find("tableFirst", 2);
                Assert.AreEqual("aa123", res2.Value[1]);
            }
        }

        private string strVarLong = @"222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
anonymous1111111111111122222222222222222222222222222222222222222222444444444455555555555555556666666666669999999
";
    }
}