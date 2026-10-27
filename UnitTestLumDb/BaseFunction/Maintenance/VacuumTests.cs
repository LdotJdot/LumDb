using System.Text;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.Maintenance
{
    [TestClass]
    public class VacuumTests
    {
        public static IEnumerable<object[]> Backends => [[TestBackend.Memory], [TestBackend.File]];

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Empty_and_fresh_database_stay_readable(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, eng =>
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true), ("v", DbValueType.Str32B, false)]);
                    ts.Insert("t", [("k", 1), ("v", "a")]);
                    ts.SaveChanges();
                }

                uint before;
                using (var ts = eng.StartTransaction())
                    before = ts.GetSpace().LastPage;

                eng.Vacuum();
                eng.Vacuum();

                using var read = eng.StartTransaction();
                Assert.AreEqual("a", read.Find("t", "k", 1).Row.GetString(1));
                Assert.IsFalse(read.GetSpace().HasFreePages);
                Assert.IsTrue(read.GetSpace().LastPage <= before);
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Deleted_rows_free_the_high_water_mark_and_ids_stay(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, (eng, path) =>
            {
                var live = new List<uint>();
                uint deleted;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("k", DbValueType.Long, true),
                        ("v", DbValueType.Int, false),
                        ("s", DbValueType.Str8B, false),
                    ]);
                    var ids = new List<uint>();
                    for (int i = 0; i < 150; i++)
                        ids.Add(ts.Insert("t", [("k", (long)i), ("v", i), ("s", "s" + (i % 7))]).Value);
                    for (int i = 0; i < 140; i++)
                        ts.Delete("t", ids[i]);
                    deleted = ids[2];
                    live.AddRange(ids.Skip(140));
                    ts.SaveChanges();
                }

                uint before;
                bool hadFree;
                using (var ts = eng.StartTransaction())
                {
                    before = ts.GetSpace().LastPage;
                    hadFree = ts.GetSpace().HasFreePages;
                }

                Assert.IsTrue(hadFree);
                eng.Vacuum();

                if (backend == TestBackend.File)
                {
                    Assert.IsFalse(File.Exists(path + ".log"));
                    Assert.IsFalse(File.Exists(path + ".vacuum.tmp"));
                    using var fs = File.Open(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(6, SeekOrigin.Begin);
                    Span<byte> ver = stackalloc byte[4];
                    Assert.AreEqual(4, fs.Read(ver));
                    Assert.AreEqual(1_003_009u, BitConverter.ToUInt32(ver));
                    var length = new FileInfo(path!).Length;
                    using var ts = eng.StartTransaction();
                    Assert.AreEqual(length, ts.GetSpace().FileBytes);
                    Assert.IsTrue(ts.GetSpace().LastPage < before);
                    Assert.IsFalse(ts.GetSpace().HasFreePages);
                }
                else
                {
                    using var ts = eng.StartTransaction();
                    Assert.IsTrue(ts.GetSpace().LastPage < before);
                    Assert.IsFalse(ts.GetSpace().HasFreePages);
                }

                using (var ts = eng.StartTransaction())
                {
                    Assert.IsFalse(ts.Find("t", deleted).IsSuccess);
                    foreach (var id in live)
                    {
                        var row = ts.Find("t", id);
                        Assert.IsTrue(row.IsSuccess, row.Exception?.Message);
                        var key = row.Row.GetLong(0);
                        Assert.AreEqual("s" + (key % 7), row.Row.GetString(2));
                        Assert.AreEqual((int)key, row.Row.GetInt(1));
                    }

                    var next = ts.Insert("t", [("k", 999L), ("v", 1), ("s", "n")]);
                    Assert.IsTrue(next.IsSuccess, next.Exception?.Message);
                    Assert.AreEqual(live.Max() + 1, next.Value);
                    ts.SaveChanges();
                }
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Gap_in_ids_is_not_reused(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, eng =>
            {
                uint a, b, c;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true)]);
                    a = ts.Insert("t", [("k", 1)]).Value;
                    b = ts.Insert("t", [("k", 2)]).Value;
                    c = ts.Insert("t", [("k", 3)]).Value;
                    ts.Delete("t", b);
                    ts.SaveChanges();
                }

                eng.Vacuum();
                using var read = eng.StartTransaction();
                Assert.IsTrue(read.Find("t", a).IsSuccess);
                Assert.IsFalse(read.Find("t", b).IsSuccess);
                Assert.IsTrue(read.Find("t", c).IsSuccess);
                var next = read.Insert("t", [("k", 4)]);
                Assert.AreEqual(c + 1, next.Value);
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Every_type_and_large_var_roundtrip(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, eng =>
            {
                var when = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                var big = new string('变', 2500);
                var bytes = Encoding.UTF8.GetBytes(big);
                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("bo", DbValueType.Bool, false),
                        ("by", DbValueType.Byte, false),
                        ("i", DbValueType.Int, true),
                        ("ui", DbValueType.UInt, false),
                        ("l", DbValueType.Long, false),
                        ("ul", DbValueType.ULong, false),
                        ("f", DbValueType.Float, false),
                        ("d", DbValueType.Double, false),
                        ("m", DbValueType.Decimal, false),
                        ("dt", DbValueType.DateTimeUTC, false),
                        ("s8", DbValueType.Str8B, false),
                        ("s16", DbValueType.Str16B, false),
                        ("s32", DbValueType.Str32B, true),
                        ("b8", DbValueType.Bytes8, false),
                        ("b16", DbValueType.Bytes16, false),
                        ("b32", DbValueType.Bytes32, false),
                        ("sv", DbValueType.StrVar, false),
                        ("bv", DbValueType.BytesVar, false),
                    ]);
                    id = ts.Insert("t",
                    [
                        ("bo", true),
                        ("by", (byte)7),
                        ("i", 11),
                        ("ui", 12u),
                        ("l", 13L),
                        ("ul", 14ul),
                        ("f", 1.5f),
                        ("d", 2.5d),
                        ("m", 3.5m),
                        ("dt", when),
                        ("s8", "s8"),
                        ("s16", "s16"),
                        ("s32", "s32"),
                        ("b8", new byte[] { 1 }),
                        ("b16", new byte[] { 2, 3 }),
                        ("b32", new byte[] { 4 }),
                        ("sv", big),
                        ("bv", bytes),
                    ]).Value;
                    ts.Insert("t",
                    [
                        ("bo", false), ("by", (byte)0), ("i", 12), ("ui", 0u), ("l", 0L), ("ul", 0ul),
                        ("f", 0f), ("d", 0d), ("m", 0m), ("dt", DateTime.UnixEpoch),
                        ("s8", ""), ("s16", ""), ("s32", "empty-key"),
                        ("b8", new byte[] { 0 }), ("b16", new byte[] { 0 }), ("b32", new byte[] { 0 }),
                        ("sv", ""), ("bv", Array.Empty<byte>()),
                    ]);
                    ts.SaveChanges();
                }

                eng.Vacuum();
                using var read = eng.StartTransaction();
                var row = read.Find("t", id);
                Assert.IsTrue(row.Row.GetBool(0));
                Assert.AreEqual(7, row.Row.GetByte(1));
                Assert.AreEqual(11, row.Row.GetInt(2));
                Assert.AreEqual(12u, row.Row.GetUInt(3));
                Assert.AreEqual(13L, row.Row.GetLong(4));
                Assert.AreEqual(14ul, row.Row.GetULong(5));
                Assert.AreEqual(1.5f, row.Row.GetFloat(6));
                Assert.AreEqual(2.5d, row.Row.GetDouble(7));
                Assert.AreEqual(3.5m, row.Row.GetDecimal(8));
                Assert.AreEqual(when, row.Row.GetDateTimeUtc(9));
                Assert.AreEqual("s8", row.Row.GetString(10));
                Assert.AreEqual("s16", row.Row.GetString(11));
                Assert.AreEqual("s32", row.Row.GetString(12));
                Assert.AreEqual(1, row.Row.GetBytes(13)[0]);
                Assert.AreEqual(big, row.Row.GetString(16));
                CollectionAssert.AreEqual(bytes, row.Row.GetBytes(17));
                Assert.AreEqual("", read.Find("t", "s32", "empty-key").Row.GetString(16));
                Assert.AreEqual(0, read.Find("t", "s32", "empty-key").Row.GetBytes(17).Length);
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Multiple_tables_and_dropped_table_stay_consistent(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, eng =>
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("a", [("k", DbValueType.Int, true)]);
                    ts.Create("b", [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
                    ts.Insert("a", [("k", 1)]);
                    ts.Insert("b", [("k", 2), ("v", "bee")]);
                    ts.Create("gone", [("k", DbValueType.Int, true)]);
                    ts.Insert("gone", [("k", 3)]);
                    ts.Drop("gone");
                    for (int i = 0; i < 30; i++)
                        ts.Update("b", "k", 2, "v", "bee" + i);
                    ts.SaveChanges();
                }

                eng.Vacuum();
                using var read = eng.StartTransaction();
                Assert.AreEqual(1, read.Find("a", "k", 1).Row.GetInt(0));
                Assert.AreEqual("bee29", read.Find("b", "k", 2).Row.GetString(1));
                Assert.IsFalse(read.GetTableNames().Values.Any(v => v.tableName == "gone"));
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Alter_then_vacuum_keeps_the_new_shape(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, (eng, path) =>
            {
                long afterAlter;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("k", DbValueType.Int, true),
                        ("blob", DbValueType.StrVar, false),
                        ("n", DbValueType.Int, false),
                    ]);
                    for (int i = 0; i < 80; i++)
                        ts.Insert("t", [("k", i), ("blob", new string('x', 200)), ("n", i)]);
                    for (int i = 0; i < 70; i++)
                        ts.Delete("t", "k", i);
                    ts.SaveChanges();
                }

                using (var ts = eng.StartTransaction())
                {
                    uint id = ts.Find("t", "k", 75).IsSuccess ? ts.Insert("t", [("k", 1000), ("blob", "z"), ("n", 1)]).Value : 0;
                    ts.Delete("t", id);
                    var kept = ts.Find("t", "k", 75);
                    Assert.IsTrue(kept.IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "flag", DbValueType.Bool, false, true, 0).IsSuccess);
                    Assert.IsTrue(ts.DropColumn("t", "n").IsSuccess);
                    ts.SaveChanges();
                    afterAlter = ts.GetSpace().FileBytes;
                }

                eng.Vacuum();
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.GetSpace().FileBytes < afterAlter);
                    var row = ts.Find("t", "k", 75);
                    Assert.IsTrue(row.Row.GetBool(0));
                    Assert.AreEqual(75, row.Row.GetInt(1));
                    Assert.AreEqual(new string('x', 200), row.Row.GetString(2));
                    Assert.IsFalse(ts.GetSpace().HasFreePages);
                    var added = ts.Insert("t", [("flag", false), ("k", 80), ("blob", "ok")]);
                    Assert.IsTrue(added.IsSuccess, added.Exception?.Message);
                    ts.SaveChanges();
                }

                if (backend == TestBackend.File)
                {
                    eng.Dispose();
                    using var again = new DbEngine(path!);
                    using var ts = again.StartTransaction();
                    Assert.AreEqual("ok", ts.Find("t", "k", 80).Row.GetString(2));
                    again.SetDestoryOnDisposed();
                }
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Vacuum_waits_for_another_thread_and_keeps_its_commit(TestBackend backend)
        {
            ColumnAlterTestsInvoke(backend, eng =>
            {
                eng.TimeoutMilliseconds = 15000;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true), ("v", DbValueType.Str32B, false)]);
                    ts.Insert("t", [("k", 1), ("v", "a")]);
                    ts.SaveChanges();
                }

                Task vacuum;
                using (var ts = eng.StartTransaction())
                {
                    vacuum = Task.Run(eng.Vacuum);
                    Thread.Sleep(300);
                    Assert.IsFalse(vacuum.IsCompleted);
                    Assert.AreEqual("a", ts.Find("t", "k", 1).Row.GetString(1));
                    var added = ts.Insert("t", [("k", 2), ("v", "b")]);
                    Assert.IsTrue(added.IsSuccess, added.Exception?.Message);
                    ts.SaveChanges();
                }

                vacuum.GetAwaiter().GetResult();

                using var read = eng.StartTransaction();
                Assert.AreEqual("a", read.Find("t", "k", 1).Row.GetString(1));
                Assert.AreEqual("b", read.Find("t", "k", 2).Row.GetString(1));
                var next = read.Insert("t", [("k", 3), ("v", "c")]);
                Assert.IsTrue(next.IsSuccess, next.Exception?.Message);
                read.SaveChanges();
            });
        }

        [TestMethod]
        public void Open_transaction_blocks_vacuum_and_file_stays()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                using var eng = new DbEngine(path);
                eng.TimeoutMilliseconds = 200;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true)]);
                    ts.Insert("t", [("k", 1)]);
                    ts.SaveChanges();
                }

                var length = new FileInfo(path).Length;
                using (var ts = eng.StartTransaction())
                {
                    Assert.ThrowsException<LumException>(() => eng.Vacuum());
                    Assert.AreEqual(1, ts.Find("t", "k", 1).Row.GetInt(0));
                }

                Assert.AreEqual(length, new FileInfo(path).Length);
                eng.SetDestoryOnDisposed();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static void ColumnAlterTestsInvoke(TestBackend backend, Action<DbEngine> body)
            => ColumnAlterTestsInvoke(backend, (eng, _) => body(eng));

        private static void ColumnAlterTestsInvoke(TestBackend backend, Action<DbEngine, string?> body)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                body(eng, path);
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }
    }
}
