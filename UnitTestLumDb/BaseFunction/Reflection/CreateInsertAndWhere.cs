using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;
using System.Diagnostics;
using System.IO;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    public class CreateInsertAndWhere
    {
        public class Student
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


        [TestMethod]
        public void Create()
        {
            const string TABLENAME = "tableFirst";

            var path = Configuration.GetRandomPath();

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create<Student>(TABLENAME);

                }

                {
                    using ITransaction ts1 = eng.StartTransaction();

                    for (int i = 0; i < 100; i++)
                    {
                        var ds1 = ts1.Insert(TABLENAME, new Student("lj", i));
                    }
                }

                using ITransaction ts2 = eng.StartTransaction();

                var ds = ts2.Find<Student>(TABLENAME, o => o.Age % 3 == 0);
             
                var ds2 = ts2.Find(TABLENAME, ("Age", o => ((int)o) % 3 == 0));
            
                Console.WriteLine(ds.Values.Count);
                Assert.IsTrue(ds.Values.Count == ds2.Values.Count);
                eng.SetDestoryOnDisposed();
            }
        }

        public class StudentInfo
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

        [TestMethod]
        public void CreateWithAttribute()
        {
            const string TABLENAME = "tableFirst";

            var path = Configuration.GetRandomPath();

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create<StudentInfo>(TABLENAME);

                }

                {
                    using ITransaction ts1 = eng.StartTransaction();

                    for (int i = 0; i < 100; i++)
                    {
                        var ds1 = ts1.Insert(TABLENAME, new Student("lj"+i.ToString(), i));
                    }
                }

                using ITransaction ts2 = eng.StartTransaction();

          
                var ds = ts2.Find<StudentInfo>(TABLENAME, o => o.Age % 3 == 0);

                foreach (var val in ds.Values)
                {
                    Console.WriteLine($"{val.Id}, {val.Name},{val.Age}");
                }

                var ds2 = ts2.Find(TABLENAME, ("Age", o => ((int)o) % 3 == 0));
                

                Console.WriteLine(ds.Values.Count);
                Assert.IsTrue(ds.Values.Count == ds2.Values.Count);
                Assert.IsTrue(ds.Values[2].Age == ds.Values[2].Id - 1);
                eng.SetDestoryOnDisposed();
            }



        }
    }
 }
