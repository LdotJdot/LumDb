using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;
using LumDbEngine.Utils.Test;
using System.Diagnostics;

namespace ConsoleTest
{
    internal class Program
    {
        /// <summary>
        /// test start from here
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            //Inserts500000Mem();

            ////
            Inserts500000();

            //Find500000();
            //Appends900000();
            Console.WriteLine("All done.");
            Console.ReadLine();
        }

        private static void Find500000()
        {
            const string TABLENAME = "tableFirst";
            {
                var st = Stopwatch.StartNew();
                using DbEngine eng = new DbEngine("d:\\tmp143701.db", false);

                using (var ts = eng.StartTransaction())
                {
                    var res = ts.Find(TABLENAME, o => o.Where(o => (int)o[0] == 499999));
                    //var res = ts.Find(TABLENAME, "a", 899999);
                    Console.WriteLine(res.Values[0][1]);
                    Console.WriteLine($"mem: {GetMem()} kb");
                }
                st.Stop();
                Console.WriteLine("elapse ms: " + st.ElapsedMilliseconds);
            }
        }

        private static void Inserts500000Mem()
        {
            const string TABLENAME = "tableFirst";
            const string strContent = "thirteen thousand one hundred fifty three";
            const string a = "a";
            const string b = "b";
            const string c = "c";
            for (int loop = 1; loop < 4; loop++)
            {
                var size = loop switch
                {
                    0 => 5000,
                    1 => 10000,
                    2 => 100000,
                    3 => 1000000
                };
                var st = Stopwatch.StartNew();
                using DbEngine eng = new DbEngine();

                using (var ts = eng.StartTransaction())
                {
                    ts.Create(TABLENAME, [(a, DbValueType.Int, true), (b, DbValueType.Long, false), (c, DbValueType.StrVar, false)]);

                    for (int i = 0; i < 500000; i++)
                    {
                        //using MemTest.MemChecker memChecker = new MemTest.MemChecker(i.ToString());

                        //Console.WriteLine("loop: "+i);
                        // todo dont know where mem increased
                        //var pageBefore= ((LumTransaction)ts).PagesCount;
                        //using var mt = new MemTest.MemChecker("outter");
                        ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", strContent)]);

                        if (i % 100000 == 0)
                        //if (i % size == 0)
                        {
                            // ts.Discard();
                        }
                        //ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one")]);
                        // Console.WriteLine($"mem: {GetMem()} kb");
                    }

                    var res = ts.Find(TABLENAME, o => o.Where(o => (int)o[0] == 499999));
                    //var res = ts.Find(TABLENAME, "a", 899999);
                    Console.WriteLine(res.Values[0][1]);
                }

                st.Stop();
                Console.WriteLine($"Mem: {GetMem()}kb, ({size}) insert done elapse ms: " + st.ElapsedMilliseconds);
                eng.Destory();
            }
        }

        private static void Inserts500000()
        {
            const string TABLENAME = "tableFirst";
            for (int loop = 0; loop < 4; loop++)
            {
                var size = loop switch
                {
                    0 => 5000,
                    1 => 10000,
                    2 => 100000,
                    3 => 1000000,
                    _ => 1
                };
                var st = Stopwatch.StartNew();
                using DbEngine eng = new DbEngine("d:\\tmp143701.db");

                using (var ts = eng.StartTransaction(0, false))
                {
                    ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
                }

                {
                    using ITransaction ts = eng.StartTransaction();

                    for (int i = 0; i < 500000; i++)
                    {
                        // using MemTest.MemChecker memChecker = new MemTest.MemChecker(i.ToString());

                        ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                        //ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one")]);
                        // Console.WriteLine(i);
                        if (i % size == 0)
                        {
                            ts.SaveChanges();
                        }
                    }
                }

                st.Stop();
                Console.WriteLine($"Mem: {GetMem()}kb, ({size}) insert done elapse ms: " + st.ElapsedMilliseconds);
                eng.Destory();
            }
        }

        private static void Appends900000()
        {
            const string TABLENAME = "tableFirst";
            {
                var st = Stopwatch.StartNew();
                using DbEngine eng = new DbEngine("d:\\tmp143701.db");

                using (var ts = eng.StartTransaction())
                {
                    ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

                    // mem increased with the  saved data number increased
                    for (int i = 500000; i < 900000; i++)
                    {
                        ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                        if (i % 10000 == 0)
                        {
                            ts.SaveChanges();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.WaitForFullGCComplete();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.WaitForFullGCComplete();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.WaitForFullGCComplete();
                            GC.Collect();
                        }
                    }
                    ;
                    ts.SaveChanges();
                }
                st.Stop();

                Console.WriteLine("insert done elapse ms: " + st.ElapsedMilliseconds);
                //  eng.Destory();
            }
        }

        public static long GetMem()
        {
            return MemTest.GetMem();
        }

        public static string GetRandomPath()
        {
            return $"{Path.GetTempPath()}{Path.GetRandomFileName()}";
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

        void IDbEntity.SetId(uint id)
        {
            this.id = id;
        }

        public override string ToString()
        {
            return $@"""id"":{id}, ""uid"":{uid}, ""username"":{username},""dec"":{dec},""time"":{time.ToLocalTime()}";
        }
    }
}