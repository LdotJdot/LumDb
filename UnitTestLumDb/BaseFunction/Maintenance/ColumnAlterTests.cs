using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using System.Text;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.Maintenance
{
    [TestClass]
    public class ColumnAlterTests
    {
        public static IEnumerable<object[]> Backends => [[TestBackend.Memory], [TestBackend.File]];

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Append_preserves_id_and_fills_default(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                uint id = Seed(eng, ("k", 7), ("name", "ab"));
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.AddColumn("t", "n", DbValueType.Int, false, 3).IsSuccess);
                    ts.SaveChanges();
                }

                Reopen(backend, ref eng, path, ts =>
                {
                    var row = ts.Find("t", id);
                    Assert.IsTrue(row.IsSuccess, row.Exception?.Message);
                    Assert.AreEqual(7, row.Row.GetInt(0));
                    Assert.AreEqual("ab", row.Row.GetString(1));
                    Assert.AreEqual(3, row.Row.GetInt(2));
                    var again = ts.Insert("t", [("k", 8), ("name", "zz"), ("n", 9)]);
                    Assert.IsTrue(again.IsSuccess, again.Exception?.Message);
                    Assert.AreEqual(9, ts.Find("t", again.Value).Row.GetInt(2));
                });
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Insert_at_front_and_middle_shifts_only_later_ordinals(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                uint id = Seed(eng, ("k", 1), ("name", "ab"));
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.AddColumn("t", "mid", DbValueType.Int, false, 5, ordinal: 1).IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "head", DbValueType.Byte, false, (byte)9, ordinal: 0).IsSuccess);
                    ts.SaveChanges();
                }

                Reopen(backend, ref eng, path, ts =>
                {
                    var row = ts.Find("t", id);
                    Assert.AreEqual(9, row.Row.GetByte(0));
                    Assert.AreEqual(1, row.Row.GetInt(1));
                    Assert.AreEqual(5, row.Row.GetInt(2));
                    Assert.AreEqual("ab", row.Row.GetString(3));
                    Assert.AreEqual(1, ts.Find("t", "k", 1).Row.GetInt(1));
                });
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Drop_first_middle_and_last(TestBackend backend)
        {
            With(backend, eng =>
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("a", DbValueType.Int, true),
                        ("b", DbValueType.Int, false),
                        ("c", DbValueType.Str8B, false),
                    ]);
                    var id = ts.Insert("t", [("a", 1), ("b", 2), ("c", "xyz")]).Value;
                    Assert.IsTrue(ts.DropColumn("t", "b").IsSuccess);
                    var row = ts.Find("t", id);
                    Assert.AreEqual(1, row.Row.GetInt(0));
                    Assert.AreEqual("xyz", row.Row.GetString(1));
                    Assert.IsTrue(ts.DropColumn("t", "c").IsSuccess);
                    Assert.AreEqual(1, ts.Find("t", id).Row.GetInt(0));
                    Assert.IsTrue(ts.AddColumn("t", "d", DbValueType.Int, false, 4).IsSuccess);
                    Assert.IsTrue(ts.DropColumn("t", "a").IsSuccess);
                    Assert.AreEqual(4, ts.Find("t", id).Row.GetInt(0));
                    Assert.ThrowsException<LumException>(() => ts.Find("t", "a", 1));
                }
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Omitted_default_is_zero_for_every_fixed_type(TestBackend backend)
        {
            With(backend, eng =>
            {
                (string name, DbValueType type)[] types =
                [
                    ("bo", DbValueType.Bool),
                    ("by", DbValueType.Byte),
                    ("i", DbValueType.Int),
                    ("ui", DbValueType.UInt),
                    ("l", DbValueType.Long),
                    ("ul", DbValueType.ULong),
                    ("f", DbValueType.Float),
                    ("d", DbValueType.Double),
                    ("m", DbValueType.Decimal),
                    ("dt", DbValueType.DateTimeUTC),
                    ("s8", DbValueType.Str8B),
                    ("s16", DbValueType.Str16B),
                    ("s32", DbValueType.Str32B),
                    ("b8", DbValueType.Bytes8),
                    ("b16", DbValueType.Bytes16),
                    ("b32", DbValueType.Bytes32),
                ];

                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true)]);
                    id = ts.Insert("t", [("k", 1)]).Value;
                    foreach (var (name, type) in types)
                        Assert.IsTrue(ts.AddColumn("t", name, type, false).IsSuccess, name);
                    ts.SaveChanges();
                }

                using var read = eng.StartTransaction();
                var row = read.Find("t", id);
                Assert.AreEqual(1, row.Row.GetInt(0));
                Assert.IsFalse(row.Row.GetBool(1));
                Assert.AreEqual(0, row.Row.GetByte(2));
                Assert.AreEqual(0, row.Row.GetInt(3));
                Assert.AreEqual(0u, row.Row.GetUInt(4));
                Assert.AreEqual(0L, row.Row.GetLong(5));
                Assert.AreEqual(0ul, row.Row.GetULong(6));
                Assert.AreEqual(0f, row.Row.GetFloat(7));
                Assert.AreEqual(0d, row.Row.GetDouble(8));
                Assert.AreEqual(0m, row.Row.GetDecimal(9));
                Assert.AreEqual(DateTime.SpecifyKind(default, DateTimeKind.Utc), row.Row.GetDateTimeUtc(10));
                Assert.AreEqual("", row.Row.GetString(11));
                Assert.AreEqual("", row.Row.GetString(12));
                Assert.AreEqual("", row.Row.GetString(13));
                Assert.IsTrue(row.Row.GetBytes(14).All(b => b == 0));
                Assert.IsTrue(row.Row.GetBytes(15).All(b => b == 0));
                Assert.IsTrue(row.Row.GetBytes(16).All(b => b == 0));
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Explicit_defaults_roundtrip(TestBackend backend)
        {
            With(backend, eng =>
            {
                var when = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true)]);
                    id = ts.Insert("t", [("k", 1)]).Value;
                    Assert.IsTrue(ts.AddColumn("t", "bo", DbValueType.Bool, false, true).IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "m", DbValueType.Decimal, false, 1.25m).IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "dt", DbValueType.DateTimeUTC, false, when).IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "s", DbValueType.Str32B, false, "hi").IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "b", DbValueType.Bytes16, false, new byte[] { 1, 2, 3 }).IsSuccess);
                    ts.SaveChanges();
                }

                using var read = eng.StartTransaction();
                var row = read.Find("t", id);
                Assert.IsTrue(row.Row.GetBool(1));
                Assert.AreEqual(1.25m, row.Row.GetDecimal(2));
                Assert.AreEqual(when, row.Row.GetDateTimeUtc(3));
                Assert.AreEqual("hi", row.Row.GetString(4));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, row.Row.GetBytes(5));
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Var_columns_survive_sibling_add_and_selective_drop(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                var big = new string('测', 3000);
                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("k", DbValueType.Int, true),
                        ("a", DbValueType.StrVar, false),
                        ("b", DbValueType.BytesVar, false),
                    ]);
                    id = ts.Insert("t", [("k", 1), ("a", big), ("b", new byte[] { 9, 8, 7 })]).Value;
                    Assert.IsTrue(ts.AddColumn("t", "n", DbValueType.Int, false, 4).IsSuccess);
                    Assert.IsTrue(ts.DropColumn("t", "b").IsSuccess);
                    ts.SaveChanges();
                }

                Reopen(backend, ref eng, path, ts =>
                {
                    var row = ts.Find("t", id);
                    Assert.AreEqual(1, row.Row.GetInt(0));
                    Assert.AreEqual(big, row.Row.GetString(1));
                    Assert.AreEqual(4, row.Row.GetInt(2));
                    Assert.AreEqual(big, ts.Find("t", "k", 1).Row.GetString(1));
                });
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void New_var_column_default_empty_and_explicit(TestBackend backend)
        {
            With(backend, eng =>
            {
                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("k", DbValueType.Int, true)]);
                    id = ts.Insert("t", [("k", 1)]).Value;
                    ts.Insert("t", [("k", 2)]);
                    Assert.IsTrue(ts.AddColumn("t", "s", DbValueType.StrVar, false).IsSuccess);
                    Assert.IsTrue(ts.AddColumn("t", "p", DbValueType.BytesVar, false, new byte[] { 1, 2 }).IsSuccess);
                    var created = ts.Insert("t", [("k", 3), ("s", "new"), ("p", new byte[] { 3 })]);
                    Assert.IsTrue(created.IsSuccess, created.Exception?.Message);
                    Assert.AreEqual("new", ts.Find("t", created.Value).Row.GetString(1));
                    ts.SaveChanges();
                }

                using var read = eng.StartTransaction();
                Assert.AreEqual("", read.Find("t", id).Row.GetString(1));
                CollectionAssert.AreEqual(new byte[] { 1, 2 }, read.Find("t", id).Row.GetBytes(2));
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Drop_key_removes_lookup_and_keeps_other_key(TestBackend backend)
        {
            With(backend, eng =>
            {
                using var ts = eng.StartTransaction();
                ts.Create("t",
                [
                    ("a", DbValueType.Int, true),
                    ("b", DbValueType.Int, true),
                    ("c", DbValueType.Int, false),
                ]);
                var id = ts.Insert("t", [("a", 1), ("b", 2), ("c", 3)]).Value;
                Assert.IsTrue(ts.DropColumn("t", "a").IsSuccess);
                Assert.ThrowsException<LumException>(() => ts.Find("t", "a", 1));
                Assert.AreEqual(3, ts.Find("t", "b", 2).Row.GetInt(1));
                Assert.AreEqual(id, ts.Update("t", id, "c", 9).IsSuccess ? id : 0);
                Assert.AreEqual(9, ts.Find("t", id).Row.GetInt(1));
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Drop_column_erases_index_keys_and_payloads(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("keep", DbValueType.Int, true),
                        ("goneKey", DbValueType.Str32B, true),
                        ("note", DbValueType.StrVar, false),
                        ("extra", DbValueType.Str32B, false),
                    ]);

                    uint id = 0;
                    for (int i = 0; i < 40; i++)
                    {
                        var inserted = ts.Insert("t",
                        [
                            ("keep", i),
                            ("goneKey", $"KEYMARKER-{i:D4}"),
                            ("note", $"VARMARKER-{i:D4}-" + new string('Q', 1200)),
                            ("extra", $"PLAINMARKER-{i:D4}"),
                        ]);
                        Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
                        if (i == 7)
                            id = inserted.Value;
                    }

                    Assert.IsTrue(ts.DropColumn("t", "goneKey").IsSuccess);
                    Assert.IsTrue(ts.DropColumn("t", "note").IsSuccess);
                    Assert.IsTrue(ts.DropColumn("t", "extra").IsSuccess);
                    Assert.ThrowsException<LumException>(() => ts.Find("t", "goneKey", "KEYMARKER-0007"));
                    Assert.AreEqual(7, ts.Find("t", "keep", 7).Row.GetInt(0));
                    Assert.AreEqual(7, ts.Find("t", id).Row.GetInt(0));
                    ts.SaveChanges();
                }

                byte[] bytes;
                if (backend == TestBackend.Memory)
                {
                    bytes = eng.CommittedImage();
                }
                else
                {
                    using var fs = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    bytes = new byte[fs.Length];
                    fs.ReadExactly(bytes);
                }
                var text = Encoding.UTF8.GetString(bytes);
                Assert.IsFalse(text.Contains("KEYMARKER-"), "dropped key index or row bytes remain");
                Assert.IsFalse(text.Contains("VARMARKER-"), "dropped var payload remains");
                Assert.IsFalse(text.Contains("PLAINMARKER-"), "dropped fixed column remains");
                Assert.IsFalse(text.Contains("goneKey"), "dropped column definition remains");
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Add_key_on_empty_or_single_row(TestBackend backend)
        {
            With(backend, eng =>
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("empty", [("k", DbValueType.Int, false)]);
                    Assert.IsTrue(ts.AddColumn("empty", "id", DbValueType.Int, true, 1).IsSuccess);
                    var ok = ts.Insert("empty", [("k", 5), ("id", 8)]);
                    Assert.IsTrue(ok.IsSuccess, ok.Exception?.Message);
                    Assert.ThrowsException<LumException>(() => ts.Insert("empty", [("k", 6), ("id", 8)]));
                }

                using (var ts = eng.StartTransaction())
                {
                    ts.Create("one", [("k", DbValueType.Int, false)]);
                    ts.Insert("one", [("k", 1)]);
                    Assert.IsTrue(ts.AddColumn("one", "id", DbValueType.Int, true, 4).IsSuccess);
                    Assert.AreEqual(1, ts.Find("one", "id", 4).Row.GetInt(0));
                    Assert.AreEqual(4, ts.Find("one", "id", 4).Row.GetInt(1));
                }
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Multi_page_rewrite_keeps_every_live_row(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                var ids = new List<uint>();
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("k", DbValueType.Long, true),
                        ("v", DbValueType.Int, false),
                        ("s", DbValueType.Str8B, false),
                    ]);
                    for (int i = 0; i < 120; i++)
                        ids.Add(ts.Insert("t", [("k", (long)i), ("v", i * 10), ("s", "s" + (i % 10))]).Value);
                    ts.Delete("t", ids[10]);
                    Assert.IsTrue(ts.AddColumn("t", "flag", DbValueType.Bool, false, true, ordinal: 1).IsSuccess);
                    ts.SaveChanges();
                }

                Reopen(backend, ref eng, path, ts =>
                {
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var row = ts.Find("t", ids[i]);
                        if (i == 10)
                        {
                            Assert.IsFalse(row.IsSuccess);
                            continue;
                        }

                        Assert.IsTrue(row.IsSuccess, i + " " + row.Exception?.Message);
                        Assert.AreEqual((long)i, row.Row.GetLong(0));
                        Assert.IsTrue(row.Row.GetBool(1));
                        Assert.AreEqual(i * 10, row.Row.GetInt(2));
                        Assert.AreEqual("s" + (i % 10), row.Row.GetString(3));
                    }

                    var count = 0;
                    ts.GoThrough("t", (uint id, ref RowView view) =>
                    {
                        count++;
                        return true;
                    });
                    Assert.AreEqual(119, count);
                });
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Other_table_is_untouched(TestBackend backend)
        {
            With(backend, eng =>
            {
                using var ts = eng.StartTransaction();
                ts.Create("a", [("k", DbValueType.Int, true)]);
                ts.Create("b", [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
                var id = ts.Insert("b", [("k", 3), ("v", "stay")]).Value;
                ts.AddColumn("a", "n", DbValueType.Int, false, 1);
                Assert.AreEqual("stay", ts.Find("b", id).Row.GetString(1));
                Assert.AreEqual(2, ts.GetTableNames().Values.Count);
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Discard_drops_uncommitted_add(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                uint id = Seed(eng, ("k", 1), ("name", "ab"));
                using (var ts = eng.StartTransaction())
                {
                    Assert.IsTrue(ts.AddColumn("t", "n", DbValueType.Int, false, 5).IsSuccess);
                    Assert.AreEqual(5, ts.Find("t", id).Row.GetInt(2));
                    ts.Discard();
                }

                Reopen(backend, ref eng, path, ts =>
                {
                    var names = ts.GetTableNames().Values.Single(v => v.tableName == "t").columns;
                    Assert.AreEqual(2, names.Length);
                    Assert.AreEqual("ab", ts.Find("t", id).Row.GetString(1));
                });
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Rejected_alter_leaves_committed_rows(TestBackend backend)
        {
            With(backend, eng =>
            {
                uint id = Seed(eng, ("k", 1), ("name", "ab"));
                using (var ts = eng.StartTransaction())
                {
                    ts.Insert("t", [("k", 2), ("name", "cd")]);
                    ts.SaveChanges();
                }

                using (var read = eng.StartTransaction())
                {
                    Assert.ThrowsException<LumException>(() => read.AddColumn("t", "k", DbValueType.Int, false));
                    Assert.ThrowsException<LumException>(() => read.AddColumn("t", "id", DbValueType.Int, true, 1));
                    Assert.ThrowsException<LumException>(() => read.AddColumn("t", "bad", DbValueType.StrVar, true));
                    Assert.ThrowsException<LumException>(() => read.AddColumn("t", "x", DbValueType.Int, false, ordinal: 9));
                    Assert.ThrowsException<LumException>(() => read.AddColumn("t", " ", DbValueType.Int, false));
                    Assert.ThrowsException<LumException>(() => read.AddColumn("t", "wide", DbValueType.Str8B, false, "too-long!"));
                    Assert.ThrowsException<LumException>(() => read.AddColumn("missing", "x", DbValueType.Int, false));
                    Assert.ThrowsException<LumException>(() => read.DropColumn("t", "nope"));
                    Assert.AreEqual("ab", read.Find("t", id).Row.GetString(1));
                    Assert.AreEqual(2, read.GetTableNames().Values.Single(v => v.tableName == "t").columns.Length);
                    read.Create("one", [("k", DbValueType.Int, true)]);
                    Assert.ThrowsException<LumException>(() => read.DropColumn("one", "k"));
                }
            });
        }

        [TestMethod]
        public void Hundredth_column_fits_and_next_is_rejected()
        {
            using var eng = new DbEngine();
            using var ts = eng.StartTransaction();
            var cols = new (string, DbValueType, bool)[1];
            cols[0] = ("c0", DbValueType.Bool, true);
            ts.Create("t", cols);
            ts.Insert("t", [("c0", true)]);
            for (int i = 1; i < 100; i++)
                Assert.IsTrue(ts.AddColumn("t", "c" + i, DbValueType.Bool, false).IsSuccess, "col " + i);
            Assert.ThrowsException<LumException>(() => ts.AddColumn("t", "overflow", DbValueType.Bool, false));
            Assert.IsTrue(ts.Find("t", "c0", true).IsSuccess);
        }

        [TestMethod]
        public void Add_is_rejected_when_the_row_would_not_fit_a_page()
        {
            using var eng = new DbEngine();
            using var ts = eng.StartTransaction();
            var cols = new (string columnName, DbValueType type, bool isKey)[77];
            var values = new (string columnName, DbCell value)[77];
            for (int i = 0; i < 77; i++)
            {
                cols[i] = ("c" + i, DbValueType.Str32B, i == 0);
                values[i] = ("c" + i, "v" + i);
            }

            Assert.IsTrue(ts.Create("t", cols).IsSuccess);
            Assert.IsTrue(ts.Insert("t", values).IsSuccess);
            Assert.ThrowsException<LumException>(() => ts.AddColumn("t", "extra", DbValueType.Str32B, false, "x"));
            Assert.AreEqual("v0", ts.Find("t", "c0", "v0").Row.GetString(0));
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Name_at_32_bytes_is_accepted(TestBackend backend)
        {
            With(backend, eng =>
            {
                var name = new string('a', 32);
                using var ts = eng.StartTransaction();
                ts.Create("t", [("k", DbValueType.Int, true)]);
                var id = ts.Insert("t", [("k", 1)]).Value;
                Assert.IsTrue(ts.AddColumn("t", name, DbValueType.Int, false, 6).IsSuccess);
                Assert.AreEqual(6, ts.Find("t", id).Row.GetInt(1));
                Assert.ThrowsException<LumException>(() => ts.AddColumn("t", new string('b', 33), DbValueType.Int, false));
            });
        }

        private static uint Seed(DbEngine eng, params (string name, DbCell value)[] extras)
        {
            using var ts = eng.StartTransaction();
            Assert.IsTrue(ts.Create("t", [("k", DbValueType.Int, true), ("name", DbValueType.Str32B, false)]).IsSuccess);
            var inserted = ts.Insert("t", [("k", extras[0].value), ("name", extras[1].value)]);
            Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
            ts.SaveChanges();
            return inserted.Value;
        }

        private static void With(TestBackend backend, Action<DbEngine> body)
            => With(backend, (eng, _) => body(eng));

        private static void With(TestBackend backend, Action<DbEngine, string?> body)
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

        private static void Reopen(TestBackend backend, ref DbEngine eng, string? path, Action<ITransaction> assert)
        {
            if (backend == TestBackend.Memory)
            {
                using var ts = eng.StartTransaction();
                assert(ts);
                return;
            }

            eng.Dispose();
            eng = new DbEngine(path!);
            using var ts2 = eng.StartTransaction();
            assert(ts2);
        }
    }
}
