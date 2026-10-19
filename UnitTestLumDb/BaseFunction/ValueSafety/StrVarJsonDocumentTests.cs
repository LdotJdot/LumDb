using System.Text.Json;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// 把 JSON 文档放进 StrVar 后读回来必须逐字节相同。
    /// 曾出现两种坏法：读出的字符串里混进页类型字节 0x06，以及插入时抛出 unknown page type: 101（即 StrVar 的类型值）。
    /// </summary>
    [TestClass]
    public class StrVarJsonDocumentTests
    {
        [TestMethod]
        public void JsonBody_RoundTripExact_AfterPerRowCommitAndReopen()
        {
            var path = Configuration.GetRandomPath();
            const int count = 240;
            var docs = BuildDocuments(count);

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create("Doc", [("Key", DbValueType.Bytes32, true), ("Body", DbValueType.StrVar, false)]).IsSuccess);
                Assert.IsTrue(ts.Create("Idx", [("Key", DbValueType.Bytes32, true), ("Id", DbValueType.StrVar, false)]).IsSuccess);
                ts.SaveChanges();
            }

            for (var i = 0; i < count; i++)
            {
                var key = Key(i);
                using var eng = Configuration.GetDbEngineForTest(path);
                using var ts = eng.StartTransaction();
                var doc = ts.Insert("Doc", [("Key", key), ("Body", docs[i])]);
                Assert.IsTrue(doc.IsSuccess, "doc " + i + " " + doc.Exception);
                var idx = ts.Insert("Idx", [("Key", key), ("Id", i.ToString())]);
                Assert.IsTrue(idx.IsSuccess, "idx " + i + " " + idx.Exception);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                for (var i = 0; i < count; i++)
                {
                    var found = ts.Find("Doc", "Key", Key(i));
                    Assert.IsTrue(found.IsSuccess, "find " + i + " " + found.Exception);
                    var body = (string)found.Value[1];
                    AssertNoControlBytes(body, i);
                    Assert.AreEqual(docs[i], body, "row " + i);
                    JsonDocument.Parse(body);
                }
                eng.SetDestoryOnDisposed();
            }

            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// 跨页后的尾巴短于 29 字节冗余时，旧实现按正文长度扣剩余空间、按 29 字节占坑。
        /// 后面的短字符串会被写成越界，读回来时混进下一页的页类型字节。
        /// </summary>
        [TestMethod]
        public void StrVar_ShortTailAcrossPages_DoesNotCorruptLaterRows()
        {
            for (var tail = 1; tail <= 28; tail++)
            {
                var path = Configuration.GetRandomPath();
                var big = new string('e', 4038 + tail);
                var filler = new string('a', 100);
                const int fillers = 60;

                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.Create("Doc", [("Key", DbValueType.Int, true), ("Body", DbValueType.StrVar, false)]).IsSuccess);
                    Assert.IsTrue(ts.Insert("Doc", [("Key", 0), ("Body", big)]).IsSuccess, "big tail " + tail);
                    for (var n = 1; n <= fillers; n++)
                    {
                        var ins = ts.Insert("Doc", [("Key", n), ("Body", filler)]);
                        Assert.IsTrue(ins.IsSuccess, "filler " + n + " tail " + tail + " " + ins.Exception);
                    }
                    ts.SaveChanges();
                }

                using (var eng = Configuration.GetDbEngineForTest(path))
                using (var ts = eng.StartTransaction())
                {
                    var foundBig = ts.Find("Doc", "Key", 0);
                    Assert.IsTrue(foundBig.IsSuccess, foundBig.Exception?.Message);
                    Assert.AreEqual(big, (string)foundBig.Value[1], "big tail " + tail);

                    for (var n = 1; n <= fillers; n++)
                    {
                        var found = ts.Find("Doc", "Key", n);
                        Assert.IsTrue(found.IsSuccess, "find " + n + " tail " + tail + " " + found.Exception);
                        var body = (string)found.Value[1];
                        AssertNoControlBytes(body, n);
                        Assert.AreEqual(filler, body, "tail " + tail + " row " + n);
                    }
                    eng.SetDestoryOnDisposed();
                }

                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void JsonBody_RoundTripExact_WhenValueCrossesVarPage()
        {
            var path = Configuration.GetRandomPath();
            var sizes = new[] { 40, 200, 800, 2000, 4500, 9000 };
            var docs = sizes.Select((size, i) => BuildOne(i, size)).ToArray();

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                Assert.IsTrue(ts.Create("Doc", [("Key", DbValueType.Bytes32, true), ("Body", DbValueType.StrVar, false)]).IsSuccess);
                for (var i = 0; i < docs.Length; i++)
                    Assert.IsTrue(ts.Insert("Doc", [("Key", Key(i)), ("Body", docs[i])]).IsSuccess, "insert " + i);
                ts.SaveChanges();
            }

            using (var eng = Configuration.GetDbEngineForTest(path))
            using (var ts = eng.StartTransaction())
            {
                for (var i = 0; i < docs.Length; i++)
                {
                    var found = ts.Find("Doc", "Key", Key(i));
                    Assert.IsTrue(found.IsSuccess, found.Exception?.Message);
                    var body = (string)found.Value[1];
                    AssertNoControlBytes(body, i);
                    Assert.AreEqual(docs[i], body, "size " + sizes[i]);
                }
                eng.SetDestoryOnDisposed();
            }

            if (File.Exists(path)) File.Delete(path);
        }

        private static void AssertNoControlBytes(string body, int row)
        {
            for (var i = 0; i < body.Length; i++)
            {
                if (body[i] < 0x20 && body[i] != '\n' && body[i] != '\r' && body[i] != '\t')
                    Assert.Fail($"row {row} has 0x{(int)body[i]:X2} at {i}");
            }
        }

        private static byte[] Key(int i)
        {
            var key = new byte[32];
            BitConverter.GetBytes(i).CopyTo(key, 0);
            return key;
        }

        private static string[] BuildDocuments(int count)
        {
            var docs = new string[count];
            for (var i = 0; i < count; i++)
                docs[i] = BuildOne(i, 180 + (i % 17) * 30);
            return docs;
        }

        private static string BuildOne(int i, int noteChars)
        {
            var note = new string('测', noteChars);
            return "{\"Id\":\"" + i.ToString("x32") + "\",\"Name\":\"阀门井" + i
                + "\",\"CreatedAt\":\"2026-10-19T08:00:00.0000000Z\",\"Note\":\"" + note + "\"}";
        }
    }
}
