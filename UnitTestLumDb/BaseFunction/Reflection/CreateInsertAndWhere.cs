using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [LumEntity]
    public partial class StudentEntity
    {
        public StudentEntity() { }

        public StudentEntity(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    [LumEntity]
    public partial class StudentInfoEntity
    {
        public StudentInfoEntity() { }

        public StudentInfoEntity(string name, int age)
        {
            Name = name;
            Age = age;
        }

        [Id]
        public uint Id { get; set; }

        [Key]
        [Str32B]
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    [TestClass]
    public class CreateInsertAndWhere
    {
        [TestMethod]
        public void Create()
        {
            const string TABLENAME = "tableFirst";
            var path = Configuration.GetRandomPath();

            using (DbEngine eng = Configuration.GetDbEngineForTest(path))
            {
                using (var ts = eng.StartTransaction(0, false))
                {
                    var res = ts.Create<StudentEntity>(TABLENAME);
                    Assert.IsTrue(res.IsSuccess);
                }

                using (ITransaction ts1 = eng.StartTransaction())
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var ds1 = ts1.Insert(TABLENAME, new StudentEntity("lj", i));
                        Assert.IsTrue(ds1.IsSuccess);
                    }
                }

                using ITransaction ts2 = eng.StartTransaction();
                var ds = ts2.Find<StudentEntity>(TABLENAME, (ref RowView r) => StudentEntity.GetAge(ref r) % 3 == 0);
                var count2 = ts2.Count(TABLENAME, (ref RowView r) => r.GetInt(1) % 3 == 0);

                Assert.AreEqual(count2.Value, (uint)ds.Values.Count());
                eng.SetDestoryOnDisposed();
            }
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
                    var res = ts.Create<StudentInfoEntity>(TABLENAME);
                    Assert.IsTrue(res.IsSuccess);
                }

                using (ITransaction ts1 = eng.StartTransaction())
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var ds1 = ts1.Insert(TABLENAME, new StudentInfoEntity("lj" + i, i));
                        Assert.IsTrue(ds1.IsSuccess);
                    }
                }

                using ITransaction ts2 = eng.StartTransaction();
                var ds = ts2.Find<StudentInfoEntity>(TABLENAME, (ref RowView r) => StudentInfoEntity.GetAge(ref r) % 3 == 0);

                foreach (var val in ds.Values)
                {
                    Console.WriteLine($"{val.Id}, {val.Name},{val.Age}");
                }

                var count2 = ts2.Count(TABLENAME, (ref RowView r) => r.GetInt(1) % 3 == 0);

                Assert.AreEqual(count2.Value, (uint)ds.Values.Count());
                var list = ds.Values.ToList();
                Assert.IsTrue(list[2].Age == list[2].Id - 1);
                eng.SetDestoryOnDisposed();
            }
        }
    }
}
