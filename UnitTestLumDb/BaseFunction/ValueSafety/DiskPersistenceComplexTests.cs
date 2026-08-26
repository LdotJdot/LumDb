using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.ValueSafety
{
    /// <summary>
    /// Complex scenarios that must persist to a real database file and survive process-style reopen.
    /// </summary>
    [TestClass]
    public class DiskPersistenceComplexTests
    {
        private static string MakeLong(int n) => new string('X', n);

        [TestMethod]
        public void Disk_MultiType_VarPages_Reopen_Find_GoThrough_Update()
        {
            var path = Configuration.GetRandomPath();
            const string table = "orders";
            var utc0 = new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc);
            string longBody = MakeLong(4000);
            byte[] blob = Encoding.UTF8.GetBytes(MakeLong(2500));

            using (var eng = new DbEngine(path))
            {
                Assert.IsTrue(File.Exists(path), "db file must be created on disk");
                using var ts = eng.StartTransaction();
                var created = ts.Create(table, [
                    ("oid", DbValueType.Int, true),
                    ("uid", DbValueType.UInt, false),
                    ("amount", DbValueType.Decimal, true),
                    ("when", DbValueType.DateTimeUTC, false),
                    ("title", DbValueType.Str32B, false),
                    ("body", DbValueType.StrVar, false),
                    ("bin", DbValueType.BytesVar, false),
                    ("flag", DbValueType.Bool, false),
                ]);
                Assert.IsTrue(created.IsSuccess, created.Exception?.Message);

                for (int i = 0; i < 200; i++)
                {
                    var ins = ts.Insert(table, [
                        ("oid", i),
                        ("uid", (uint)(1000 + i)),
                        ("amount", 10.5000m + i),
                        ("when", utc0.AddMinutes(i)),
                        ("title", "t" + i),
                        ("body", i == 50 ? longBody : "short-" + i),
                        ("bin", i == 51 ? blob : new byte[] { (byte)i }),
                        ("flag", i % 2 == 0),
                    ]);
                    Assert.IsTrue(ins.IsSuccess, $"insert {i}: " + ins.Exception?.Message);
                }

                // Same-session read before close (isolates reopen vs insert)
                var sameSession = ts.Find(table, "oid", 50);
                Assert.IsTrue(sameSession.IsSuccess, "same-session find: " + sameSession.Exception?.Message);
                Assert.AreEqual(longBody, sameSession.Row.GetString(5));

                ts.SaveChanges();
            }

            var lenAfterWrite = new FileInfo(path).Length;
            Assert.IsTrue(lenAfterWrite > 0);

            using (var eng = new DbEngine(path, createIfNotExists: false))
            using (var ts = eng.StartTransaction())
            {
                var shortRow = ts.Find(table, "oid", 3);
                Assert.IsTrue(shortRow.IsSuccess, shortRow.Exception?.Message);
                Assert.IsInstanceOfType(shortRow.Row, typeof(DbRowBuffer));
                Assert.AreEqual(3, shortRow.Row.GetInt(0));
                Assert.AreEqual("short-3", shortRow.Row.GetString(5));

                var byKey = ts.Find(table, "oid", 50);
                Assert.IsTrue(byKey.IsSuccess, "reopen find long row: " + byKey.Exception?.Message);
                Assert.AreEqual(50, byKey.Row.GetInt(0));
                Assert.AreEqual(1050u, byKey.Row.GetUInt(1));
                Assert.AreEqual(0, decimal.Compare(60.5000m, byKey.Row.GetDecimal(2)));
                Assert.AreEqual(utc0.AddMinutes(50), byKey.Row.GetDateTimeUtc(3));
                Assert.AreEqual("t50", byKey.Row.GetString(4));
                Assert.AreEqual(longBody, byKey.Row.GetString(5));
                Assert.AreEqual(true, byKey.Row.GetBool(7));

                var byAmount = ts.Find(table, "amount", 15.5000m);
                Assert.IsTrue(byAmount.IsSuccess);
                Assert.AreEqual(5, byAmount.Row.GetInt(0));

                int scanned = 0;
                long oidSum = 0;
                ts.GoThrough(table, (ref RowView row) =>
                {
                    oidSum += row.GetInt(0);
                    scanned++;
                    return true;
                });
                Assert.AreEqual(200, scanned);
                Assert.AreEqual(200 * 199 / 2, oidSum);

                var upd = ts.Update(table, "oid", 50, "body", MakeLong(3000));
                Assert.IsTrue(upd.IsSuccess, upd.Exception?.Message);
                var updTitle = ts.Update(table, "oid", 50, "title", "updated");
                Assert.IsTrue(updTitle.IsSuccess);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                var row = ts.Find(table, "oid", 50);
                Assert.IsTrue(row.IsSuccess);
                Assert.AreEqual("updated", row.Row.GetString(4));
                Assert.AreEqual(MakeLong(3000), row.Row.GetString(5));
                CollectionAssert.AreEqual(blob, ts.Find(table, "oid", 51).Row.GetBytes(6));

                eng.SetDestoryOnDisposed();
            }

            Assert.IsFalse(File.Exists(path), "SetDestoryOnDisposed should remove the db file");
        }

        [TestMethod]
        public void Disk_Entity_WriteTo_TryReadFrom_RoundTrip()
        {
            var path = Configuration.GetRandomPath();
            const string table = "people";

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create(table, [
                    ("uid", DbValueType.Int, true),
                    ("name", DbValueType.Str32B, false),
                    ("score", DbValueType.Long, false),
                ]);

                for (int i = 0; i < 30; i++)
                {
                    var r = ts.Insert_Entity(table, new PersonEntity
                    {
                        Uid = i,
                        Name = "p" + i,
                        Score = i * 10L,
                    });
                    Assert.IsTrue(r.IsSuccess, r.Exception?.Message);
                }
                ts.SaveChanges();
            }

            Assert.IsTrue(File.Exists(path));

            using (var eng = new DbEngine(path, createIfNotExists: false))
            using (var ts = eng.StartTransaction())
            {
                var found = ts.Find_Entity<PersonEntity>(table, "uid", 12);
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual(12, found.Value.Uid);
                Assert.AreEqual("p12", found.Value.Name);
                Assert.AreEqual(120L, found.Value.Score);

                var upd = ts.Update_Entity(table, 13u, new PersonEntity { Uid = 12, Name = "renamed", Score = 999 });
                // update by id: id 13 is the 13th inserted row (1-based), whose uid was 12
                // Actually Insert auto id: first insert id=1 with uid=0. So uid=12 has id=13.
                Assert.IsTrue(upd.IsSuccess, upd.Exception?.Message);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                var found = ts.Find_Entity<PersonEntity>(table, "uid", 12);
                // Update_Entity by id updates columns from entity including uid field in WriteTo —
                // our Update writes all columns from entity with Uid=12 still. Name should be renamed.
                // Wait - we used id 13u with entity Uid=12. Update writes all column values from entity.
                Assert.IsTrue(found.IsSuccess);
                Assert.AreEqual("renamed", found.Value.Name);
                Assert.AreEqual(999L, found.Value.Score);
                eng.SetDestoryOnDisposed();
            }
        }

        [TestMethod]
        public void Disk_TwoTables_Delete_And_ReopenConsistency()
        {
            var path = Configuration.GetRandomPath();

            using (var eng = new DbEngine(path))
            using (var ts = eng.StartTransaction())
            {
                ts.Create("a", [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
                ts.Create("b", [("k", DbValueType.Int, true), ("v", DbValueType.Int, false)]);
                for (int i = 0; i < 40; i++)
                {
                    ts.Insert("a", [("k", i), ("v", "va-" + i)]);
                    ts.Insert("b", [("k", i), ("v", i * 3)]);
                }
                ts.Delete("a", "k", 5);
                ts.Delete("b", "k", 7);
                ts.SaveChanges();
            }

            using (var eng = new DbEngine(path, createIfNotExists: false))
            {
                using var ts = eng.StartTransaction();
                Assert.IsFalse(ts.Find("a", "k", 5).IsSuccess);
                Assert.IsTrue(ts.Find("a", "k", 6).IsSuccess);
                Assert.AreEqual("va-6", ts.Find("a", "k", 6).Row.GetString(1));
                Assert.IsFalse(ts.Find("b", "k", 7).IsSuccess);
                Assert.AreEqual(24, ts.Find("b", "k", 8).Row.GetInt(1));

                int countA = 0;
                ts.GoThrough("a", (ref RowView row) => { countA++; return true; });
                Assert.AreEqual(39, countA);

                eng.SetDestoryOnDisposed();
            }
        }

        private sealed class PersonEntity : IDbEntity
        {
            public int Uid;
            public string Name = "";
            public long Score;
            public uint Id;

            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteInt(Uid);
                writer.WriteString(Name);
                writer.WriteLong(Score);
            }

            public bool TryReadFrom(IDbRow row)
            {
                Uid = row.GetInt(0);
                Name = row.GetString(1);
                Score = row.GetLong(2);
                return true;
            }

            public void GetId(uint id) => Id = id;
        }
    }
}
