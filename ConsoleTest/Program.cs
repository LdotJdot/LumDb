using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Value;
using LumDbEngine.Utils.Test;
using System.Diagnostics;

namespace ConsoleTest
{
    public class DbProjAuthority : IDbEntity
    {
        public uint id { get; set; }
        public byte[] authority { get; set; }
        public uint userid { get; set; }
        public uint projid { get; set; }
        public int role { get; set; }
        public DbProjAuthority()
        {

        }

        public DbProjAuthority(uint userid, uint projid, int pur)
        {
            authority = GetKey(userid, projid);
            this.userid = userid;
            this.projid = projid;
            role = (int)pur;
        }

        internal byte[] Key => GetKey(userid, projid);
        internal static byte[] GetKey(uint userid, uint projid)
        {
            var bt = new byte[8];
            BitConverter.TryWriteBytes(bt.AsSpan(0, 4), userid);
            BitConverter.TryWriteBytes(bt.AsSpan(4, 4), projid);
            return bt;
        }

        public IDbEntity Unboxing(object[] obj)
        {
            authority = (byte[])obj[0];
            userid = (uint)obj[1];
            projid = (uint)obj[2];
            role = (int)obj[3];
            return this;
        }

        public void GetId(uint id)
        {
            this.id = id;
        }

        public object[] Boxing()
        {
            return [authority, userid, projid, role];
        }
    }
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
            Debug();
            //readWriteLock();


            Console.WriteLine("All done.");
            Console.ReadLine();
        }
            
                    public const string TableName = "projAuthority";
        private static void Debug()
        {
            using DbEngine eng = new DbEngine(@"D:\Data\个人\FAV\MyCoreProj\WorkInProcess\ProjectNexus\PNWebHost\PNWebHost\bin\x64\Debug\net8.0\DbProjs\PNNexus.db");
            using var ts = eng.StartTransaction();

        var res = ts.Find(TableName, o=>o);
        
            ;

            var bt = new byte[8];
            BitConverter.TryWriteBytes(bt.AsSpan(0, 4), 3);
            BitConverter.TryWriteBytes(bt.AsSpan(4, 4), 1);

            var ss = ts.Find(TableName, "authority", bt);
            ;
            //var res3 = ts.Find(TableName, o => o.Where(o => (int)o[2] == 1));
            var res3 = ts.Find<DbProjAuthority>(TableName, o => o.Where(p => p.projid == 1));

            ;


        }
        private static void readWriteLock()
        {
            using DbEngine eng = new DbEngine("d:\\tmp143701.db");
            using DbEngine en2 = new DbEngine("d:\\tmp143702.db");
            const string TABLENAME = "tableFirst";


            using (var ts2 = eng.StartTransaction(0, false))
            {
                using var tt6=en2.StartTransaction();
                //ts2.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
               // var ds = ts2.Insert(TABLENAME, [("a", 22), ("b", (long)233), ("c", "thirteen thousand one hundred fifty three")]);
            }


            using var ts = eng.StartTransaction();
            var res = ts.Find(TABLENAME, 1);
            //using var ts3 = eng.StartTransaction();

            var t =Task.Factory.StartNew(() =>
            {
                // The following code would be block due to the singularity of transaction. And should be not called in same thread.
                using var ts3 = eng.StartTransaction();
                ts3.Insert(TABLENAME, [("a", 55), ("b", (long)233), ("c", "thirteen thousand one hundred fifty three")]);
                var res3 = ts3.Find(TABLENAME, 2);
                Console.WriteLine("r3"+res3.Value[0].ToString());
            });
            ts.Dispose();

            t.Wait();
            Console.WriteLine(res.Value[0]);
        }
        private static void Inserts50()
        {
            const string TABLENAME = "tableFirst";
              
                using DbEngine eng = new DbEngine("d:\\tmp143701.db");

                using (var ts = eng.StartTransaction(0, false))
                {
                    ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
                }

                {
                    using ITransaction ts = eng.StartTransaction();

                    for (int i = 0; i < 500; i++)
                    {
                        // using MemTest.MemChecker memChecker = new MemTest.MemChecker(i.ToString());

                        var ds = ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                        //ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one")]);
                        // Console.WriteLine(i);
                    }
                }

            {
                using ITransaction ts = eng.StartTransaction();

                var res = ts.Find(TABLENAME, o => o);

                foreach(var r in res.Values)
                {
                    Console.WriteLine(r[0]);
                }
            }

            eng.Destory();
            
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

                       var ds= ts.Insert(TABLENAME, [("a", i), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
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