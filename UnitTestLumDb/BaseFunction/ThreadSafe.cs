using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class ThreadSafe
    {
        [TestMethod]
        public void InsertAndFindInParallelInSeparatedTransaction()
        {
            var path = Configuration.GetRandomPath();

            const string TABLENAME = "tableFirst";

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                {
                    using var ts = eng.StartTransaction();
                    ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);
                }

                //Parallel.For(0, 1000, i =>
                for (int i = 0; i < 1000; i++)
                {
                    using var ts = eng.StartTransaction();
                    ts.Insert<TestVar>(TABLENAME, new TestVar() { uid = i, username = i + ":123", content = i + "i" });
                    var res = ts.Find<TestVar>(TABLENAME, (uint)i + 1);
                    Assert.IsTrue(res.IsSuccess);
                }
                //);

                long sumi = 0;
                var lk = new object();

                Parallel.For(0, 1000, i =>
                    {
                        using var ts = eng.StartTransaction();
                        var res = ts.Find<TestVar>(TABLENAME, (uint)i + 1);
                        lock (lk)
                        {
                            sumi += res.Value.id;
                        }
                    });
                Assert.AreEqual(sumi, 500500);
                eng.Destory();
            }
        }

        [TestMethod]
        public void InsertAndFindInParallelInOneTransaction()
        {
            var path = Configuration.GetRandomPath();

            const string TABLENAME = "tableFirst";

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                {
                    using var ts = eng.StartTransaction();
                    ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

                    Parallel.For(0, 1000, i =>
                    {
                        ts.Insert<TestVar>(TABLENAME, new TestVar() { uid = i, username = i + ":123", content = i + "i" });
                    });

                    long sumi = 0;
                    var lk = new object();
                    Parallel.For(0, 1000, i =>
                    {
                        var res = ts.Find<TestVar>(TABLENAME, (uint)i + 1);
                        lock (lk)
                        {
                            sumi += res.Value.id;
                        }
                    });
                    Assert.AreEqual(sumi, 500500);
                }
                eng.Destory();
            }
        }

        [TestMethod]
        public void InsertAndFindAtSameTimeInOneTransaction()
        {
            var path = Configuration.GetRandomPath();

            const string TABLENAME = "tableFirst";

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                {
                    using var ts = eng.StartTransaction();
                    ts.Create(TABLENAME, [("uid", DbValueType.Int, true), ("username", DbValueType.Str32B, true), ("content", DbValueType.StrVar, false)]);

                    var t1 = Task.Run(() =>
                    {
                        Thread.Sleep(5);

                        Parallel.For(0, 1000, i =>
                        {
                            ts.Insert<TestVar>(TABLENAME, new TestVar() { uid = i, username = i + ":123", content = i + "i" });
                        });
                    });

                    var t2 = Task.Run(() =>
                    {
                        long sumi = 0;
                        var lk = new object();
                        Parallel.For(0, 1000, i =>
                        {
                            var res = ts.Find<TestVar>(TABLENAME, (uint)i + 1);
                            if (res.IsSuccess)
                            {
                                lock (lk)
                                {
                                    sumi += res.Value.id;
                                }
                            }
                        });
                        Console.WriteLine("sumi: " + sumi);
                        Assert.IsTrue(sumi >= 0 && sumi <= 500500);
                    });
                    Task.WaitAll(t1, t2);

                    long sumi = 0;
                    var lk = new object();
                    Parallel.For(0, 1000, i =>
                    {
                        var res = ts.Find<TestVar>(TABLENAME, (uint)i + 1);
                        lock (lk)
                        {
                            sumi += res.Value.id;
                        }
                    });
                    Assert.AreEqual(sumi, 500500);
                }
                eng.Destory();
            }
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

            void IDbEntity.SetId(uint id)
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