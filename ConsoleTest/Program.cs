using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.Test;
using PNMessage;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Xml.Linq;

namespace ConsoleTest
{
   

    internal partial class Program
    {


        private static void Bug()
        {
            using DbEngine eng = new DbEngine("queryCache.db");
        }

        /// <summary>
        /// test start from here
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            NexusMart.NexusMartSimulation.Run(args);
        }

        [LumEntity]
        public partial class StudentInfo
        {

            public StudentInfo()
            {

            }

            public StudentInfo(string name, int age)
            {
                Name = name;
                Age = age;
            }

            [Id]
            public uint Id { get; set; }

            [Key]
            [Str32B]
            public string Name { get; set; } = "";

            public int Age { get; set; } = 0;
        }

        [LumEntity]
        public partial class Student
        {

            public Student()
            {

            }

            public Student(string name, int age)
            {
                Name = name;
                Age = age;
            }

            public string Name { get; set; } = "";
            public int Age { get; set; } = 0;
        }
        private static void MemTable()
        {
            const string TABLENAME_1 = "tableFirst";
            const string TABLENAME_2 = "tableSecond";
            using (DbEngine eng = new DbEngine())

            {
                using (var tsCreate = eng.StartTransaction(0, false))
                {
                    tsCreate.Create<Student>(TABLENAME_1);
                    tsCreate.Create<StudentInfo>(TABLENAME_2);
                }

                using (ITransaction ts = eng.StartTransaction())
                {
                    for (int i = 0; i < 1; i++)
                    {
                        var r1=ts.Insert(TABLENAME_1, new Student("lj" + i.ToString(), i));
                        var r2=ts.Insert(TABLENAME_2, new StudentInfo(i.ToString() + "lj", i));
                    }

                    // ts.Dispose();  // manually committed.
                    // ts.Discard();
                } // transaction will be auto committed

            }
        }

        private static void ReflectorInsert()
        {
            const string TABLENAME_1 = "tableFirst";
            const string TABLENAME_2 = "tableSecond";
            {
                using (DbEngine eng1 = new DbEngine("d:\\xxxxReflectorInsert.db", true))
                {

                    eng1.TimeoutMilliseconds = 10000; // set timeout to 10 seconds

                    using (var tsCreate = eng1.StartTransaction(0, false))
                    {
                        tsCreate.Create<Student>(TABLENAME_1);
                        tsCreate.Create<StudentInfo>(TABLENAME_2);
                    }
                }

                using DbEngine eng = new DbEngine("d:\\xxxxReflectorInsert.db");
                using (ITransaction ts = eng.StartTransaction())
                {
                    for (int i = 0; i < 100; i++)
                    {
                        ts.Insert(TABLENAME_1, new Student("lj" + i.ToString(), i));
                        ts.Insert(TABLENAME_2, new StudentInfo(i.ToString() + "lj", i));
                    }

                    // ts.Dispose();  // manually committed.
                    // ts.Discard();
                } // transaction will be auto committed



                using (ITransaction ts2 = eng.StartTransaction())
                {
                    var ds1 = ts2.Find<Student>(TABLENAME_1, (ref RowView r) => r.GetInt(1) > 98);

                    foreach (var val in ds1.Values)
                    {
                        Console.WriteLine(val.Name + " "+ val.Age.ToString());
                    }

                    ts2.GoThrough(TABLENAME_1, (ref RowView r) =>
                    {
                        if (r.GetInt(1) < 2)
                            Console.WriteLine(r.GetString(0) + " " + r.GetInt(1));
                        return true;
                    });

                    var ds3 = ts2.Find<StudentInfo>(TABLENAME_2, (ref RowView r) => r.GetInt(1) % 17 == 0);

                    foreach (var val in ds3.Values)
                    {
                        Console.WriteLine($"{val.Id}, {val.Name},{val.Age}");
                    }
                }

                eng.SetDestoryOnDisposed();


                Console.WriteLine("All complete");




            }
        }

        private static void AsyncRead()
        {
            const string TABLENAME = "tableFirst";
            {
                using DbEngine eng = new DbEngine("d:\\xxxxAsyncRead.db", true);
                eng.TimeoutMilliseconds = 10000; // set timeout to 10 seconds

                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

                }

                //{
                //    using ITransaction ts = eng.StartTransaction();

                //    for (int i = 0; i < 1000; i++)
                //    {
                //        var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                //    }
                //}





                using (var tsWrite = eng.StartTransaction())
                {
                    tsWrite.SaveChanges();

                    Console.WriteLine("tsW started");
                    using (var tsWrite2 = eng.StartTransaction())
                    {

                        Console.WriteLine("tsW2 started");
                        tsWrite2.SaveChanges();
                        var resW2 = tsWrite2.Find(TABLENAME, 1);
                        Console.WriteLine("tsW2" + resW2.Value[0]);
                        
                        Console.WriteLine("tsW2 complete");
                        tsWrite2.SaveChanges();

                    }

                    tsWrite.SaveChanges();

                    var res = tsWrite.Find(TABLENAME, 2);
                    Console.WriteLine("tsW" + res.Value[0]);
                    Console.WriteLine("tsW complete");
                   
                }


                Console.WriteLine("All complete");


                //  eng.SetDestoryOnDisposed();


            }
        }

        private static void GetTableNames()
        {
            const string TABLENAME = "tableFirst";
            {
                using DbEngine eng = new DbEngine("d:\\xxxxTableName.db", true);

                for(int i = 0; i < 1; i++)
                {

                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create(TABLENAME+i.ToString(), [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

                }
                }
                using var ts2 = eng.StartTransaction(0, false);
                var re = ts2.GetTableNames();

                foreach (var item in re.Values)
                {
                    Console.WriteLine(item.tableName);
                    Console.WriteLine("{");
                    foreach(var col in item.columns)
                    {
                        Console.WriteLine(col.columnName);
                        Console.WriteLine(col.dataType);
                        Console.WriteLine(col.isKey);
                    }
                    Console.WriteLine("}");
                }
            }
        }

        private static void Gothrough()
        {
            const string TABLENAME = "tableFirst";
            {
                using DbEngine eng = new DbEngine("d:\\xxxxGothrough.db", true);


                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

                }

                {
                    using ITransaction ts = eng.StartTransaction();

                    for (int i = 0; i < 1000; i++)
                    {
                        var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                    }
                }                               

                int count = 0;

                {
                    using ITransaction ts = eng.StartTransaction();

                        ts.GoThrough(TABLENAME, (ref RowView r) =>
                        {
                            count++;
                            Console.WriteLine(r.GetInt(0));
                            if (count > 500) return false;
                            return true;
                        });
                }

                Console.WriteLine(count);

                eng.SetDestoryOnDisposed();


            }



        }


        private static void WhereMethod()
        {
            const string TABLENAME = "tableFirst";
            {
                using DbEngine eng = new DbEngine("d:\\xxxxWhere.db", true);


                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

                }

                {
                    using ITransaction ts = eng.StartTransaction();

                    for (int i = 0; i < 1000; i++)
                    {
                        var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                    }
                }
            }


            //{
            //    using DbEngine eng = new DbEngine("d:\\xxxxWhere.db", false);
            //    using ITransaction ts = eng.StartTransaction();

            //    var t2 = Stopwatch.GetTimestamp();

            //    var ds = ts.Find(TABLENAME, o => (o.Take(5000)));
            //    Console.WriteLine(ds.Values.Count);

            //    Console.WriteLine(Stopwatch.GetTimestamp() - t2);
            //    eng.SetDestoryOnDisposed();

            //}

            {
                using DbEngine eng = new DbEngine("d:\\xxxxWhere.db", false);
                using ITransaction ts = eng.StartTransaction();

                var t = Stopwatch.GetTimestamp();
                int taken = 0;
                ts.GoThrough(TABLENAME, (ref RowView r) =>
                {
                    if (taken == 0)
                        Console.WriteLine(r.GetInt(0));
                    taken++;
                    return taken < 500;
                });
                Console.WriteLine(taken);
                Console.WriteLine("value");

                Console.WriteLine(Stopwatch.GetTimestamp() - t);
                eng.SetDestoryOnDisposed();

            }
        }
        private static void CountMethod()
        {
            const string TABLENAME = "tableFirst";
            {
                using DbEngine eng = new DbEngine("d:\\xxxxCount.db", true);


                using (var ts = eng.StartTransaction(0, false))
                {
                   var res = ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);

                }

                {
                    using ITransaction ts = eng.StartTransaction();

                    for (int i = 0; i < 10000; i++)
                    {
                        var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                    }
                }
            }


            //{
            //    using DbEngine eng = new DbEngine("d:\\xxxxCount.db", false);
            //    using ITransaction ts = eng.StartTransaction();

            //    var t2 = Stopwatch.GetTimestamp();

            //    var ds = ts.Find(TABLENAME, o => (o.Where(l => ((long)l[1]) % 3 == 0 && (int)l[0]>5000)));
            //    Console.WriteLine(ds.Values.Count);

            //    Console.WriteLine(Stopwatch.GetTimestamp() - t2);
            //    eng.SetDestoryOnDisposed();

            //}

        {
            using DbEngine eng = new DbEngine("d:\\xxxxCount.db", false);
            using ITransaction ts = eng.StartTransaction();

            var t = Stopwatch.GetTimestamp();
            var ds2 = ts.Count(TABLENAME, (ref RowView r) => r.GetLong(1) % 3 == 0 && r.GetInt(0) > 5000);
           // Console.WriteLine(ds2.Value);

            Console.WriteLine(Stopwatch.GetTimestamp() - t);
            eng.SetDestoryOnDisposed();

        }

    }


        private static void AtomicCheck()
        {
            const string TABLENAME = "tableFirst";
            {
                using DbEngine eng = new DbEngine("d:\\xxxx123.db", true);


                using (var ts = eng.StartTransaction(0, false))
                {
                    // var res=ts.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
                }

                {
                    using ITransaction ts = eng.StartTransaction();

                    for (int i = 0; i < 5; i++)
                    {
                        //var ds = ts.Insert(TABLENAME, [("a", i + 500), ("b", (long)i * i), ("c", "thirteen thousand one hundred fifty three")]);
                    }
                }
            }

            {
                using DbEngine eng = new DbEngine("d:\\xxxx123.db",false);
                using ITransaction ts = eng.StartTransaction();


                ts.GoThrough(TABLENAME, (ref RowView r) =>
                {
                    Console.WriteLine(r.GetInt(0));
                    return false; // sample first row only
                });


                //  eng.SetDestoryOnDisposed();
            }
        }
        private static void destory()
        {

            using DbEngine eng = new DbEngine("d:\\xxxx123.db");
            Task.Run(() =>
            {
                try
                {               
                    using var ts = eng.StartTransaction();
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            });
            Thread.Sleep(500);
            eng.TimeoutMilliseconds = 0;
            eng.SetDestoryOnDisposed();
            eng.Dispose();
        }

        private static void Debug()
        {
            using DbEngine eng = new DbEngine("d:\\tmp143704.db");
            using var ts=eng.StartTransaction();

            using var ts2=eng.StartTransaction();

            string tb1 = "projAuthority";
            string tb2 = "projAuthority2";

            //ts.Create(tb1, [("a", DbValueType.Int, true)]);
            //ts.Create(tb2, [("a", DbValueType.Int, true)]);

            //for (int i = 0; i < 10; i++)
            //{
            //    ts.Insert(tb1, [("a", i)]);
            //}
            //for (int i = 0; i < 5; i++)
            //{
            //    ts.Insert(tb2, [("a", i)]);
            //}

            var set2 = new HashSet<int>();
            ts.GoThrough(tb2, (ref RowView r) =>
            {
                set2.Add(r.GetInt(0));
                return true;
            });

            ts.GoThrough(tb1, (ref RowView r) =>
            {
                if (set2.Contains(r.GetInt(0)))
                    Console.WriteLine(r.GetInt(0));
                return true;
            });

        }
        private static void readWriteLock()
        {
            using DbEngine eng = new DbEngine("d:\\tmp143701.db");
            using DbEngine en2 = new DbEngine("d:\\tmp143702.db");
            const string TABLENAME = "tableFirst";


            using (var ts2 = eng.StartTransaction(0, false))
            {


                //ts2.Create(TABLENAME, [("a", DbValueType.Int, true), ("b", DbValueType.Long, false), ("c", DbValueType.StrVar, false)]);
               // var ds = ts2.Insert(TABLENAME, [("a", 22), ("b", (long)233), ("c", "thirteen thousand one hundred fifty three")]);
            }


            using var ts = eng.StartTransaction();
            var res = ts.Find(TABLENAME, 1);


            var t =Task.Factory.StartNew(() =>
            {
                // The following code would be block due to the singularity of transaction. And should be not called in same thread.

                using var ts3 = eng.StartTransaction();
               // ts3.Insert(TABLENAME, [("a", 55), ("b", (long)233), ("c", "thirteen thousand one hundred fifty three")]);
                var res3 = ts3.Find(TABLENAME, 2);
                Console.WriteLine("r3"+res3.Value[0].ToString());
            });

            Thread.Sleep(6000);
            Console.WriteLine("ts.done");

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

                ts.GoThrough(TABLENAME, (ref RowView r) =>
                {
                    Console.WriteLine(r.GetInt(0));
                    return true;
                });
            }

            eng.SetDestoryOnDisposed();
            
        }
        private static void Find500000()
        {
            const string TABLENAME = "tableFirst";
            {
                var st = Stopwatch.StartNew();
                using DbEngine eng = new DbEngine("d:\\tmp143701.db", false);

                using (var ts = eng.StartTransaction())
                {
                    var res = ts.Find(TABLENAME, "a", 499999);
                    //var res = ts.Find(TABLENAME, "a", 899999);
                    Console.WriteLine(res.Row.GetLong(1));
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

                    var res = ts.Find(TABLENAME, "a", 499999);
                    //var res = ts.Find(TABLENAME, "a", 899999);
                    Console.WriteLine(res.Row.GetLong(1));
                }

                st.Stop();
                Console.WriteLine($"Mem: {GetMem()}kb, ({size}) insert done elapse ms: " + st.ElapsedMilliseconds);
                eng.SetDestoryOnDisposed();
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
                eng.SetDestoryOnDisposed();
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

            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteInt(uid);
                writer.WriteString(username);
            }

            public bool TryReadFrom(IDbRow row)
            {
                uid = row.GetInt(0);
                username = row.GetString(1);
                return true;
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

        public void WriteTo(ref RowWriter writer)
        {
            writer.WriteInt(uid);
            writer.WriteString(username);
            writer.WriteString(content);
        }

        public bool TryReadFrom(IDbRow row)
        {
            uid = row.GetInt(0);
            username = row.GetString(1);
            content = row.GetString(2);
            return true;
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

        public void WriteTo(ref RowWriter writer)
        {
            writer.WriteInt(uid);
            writer.WriteString(username);
            writer.WriteDecimal(dec);
            writer.WriteDateTimeUtc(time);
        }

        public bool TryReadFrom(IDbRow row)
        {
            uid = row.GetInt(0);
            username = row.GetString(1);
            dec = row.GetDecimal(2);
            time = row.GetDateTimeUtc(3);
            return true;
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