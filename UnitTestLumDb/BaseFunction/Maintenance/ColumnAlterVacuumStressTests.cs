using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.Maintenance
{
    /// <summary>
    /// Repeated add/drop, mixed with insert/update/delete, reopen, and vacuum.
    /// An in-memory oracle is the source of truth. Assertions stay strict on purpose.
    /// </summary>
    [TestClass]
    public class ColumnAlterVacuumStressTests
    {
        public static IEnumerable<object[]> Backends => [[TestBackend.Memory], [TestBackend.File]];

        private enum Kind { Int, Bool, Str32, StrVar, Bytes }

        private sealed class Col
        {
            public string Name = "";
            public Kind Kind;
            public bool IsKey;
            public DbValueType Type => Kind switch
            {
                Kind.Int => DbValueType.Int,
                Kind.Bool => DbValueType.Bool,
                Kind.Str32 => DbValueType.Str32B,
                Kind.StrVar => DbValueType.StrVar,
                Kind.Bytes => DbValueType.BytesVar,
                _ => throw new InvalidOperationException(),
            };
        }

        private sealed class Row
        {
            public uint Id;
            public Dictionary<string, object> Values = new();
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Repeated_add_drop_update_delete_vacuum_matches_oracle(TestBackend backend)
        {
            var rng = new Random(20261026);
            var cols = new List<Col>
            {
                new() { Name = "kid", Kind = Kind.Int, IsKey = true },
                new() { Name = "note", Kind = Kind.Str32, IsKey = false },
            };
            var rows = new Dictionary<uint, Row>();
            var dead = new HashSet<uint>();
            uint maxId = 0;
            var seq = 0;
            var other = new Dictionary<uint, int>();

            With(backend, (eng, path) =>
            {
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", Schema(cols));
                    ts.Create("other", [("k", DbValueType.Int, true), ("v", DbValueType.StrVar, false)]);
                    for (int i = 0; i < 5; i++)
                    {
                        var body = "other-" + new string('稳', 800) + i;
                        var id = ts.Insert("other", [("k", i), ("v", body)]).Value;
                        other[id] = i;
                    }

                    for (int i = 0; i < 24; i++)
                        InsertRow(ts, cols, rows, ref maxId, rng, ref seq);
                    ts.SaveChanges();
                }

                for (int round = 0; round < 18; round++)
                {
                    using (var ts = eng.StartTransaction())
                    {
                        if (cols.Count < 7)
                        {
                            Add(ts, cols, rows, rng, ref seq, ordinal: 0);
                            Add(ts, cols, rows, rng, ref seq, ordinal: rng.Next(0, cols.Count + 1));
                        }

                        foreach (var row in rows.Values.Where((_, i) => i % 2 == round % 2).Take(6))
                        {
                            foreach (var col in cols.Where(c => c.Name != "kid").Take(2))
                            {
                                var updated = FreshValue(col, row, round, rng);
                                Assert.IsTrue(ts.Update("t", row.Id, col.Name, ToCell(col, updated)).IsSuccess, col.Name + " round " + round);
                                row.Values[col.Name] = updated;
                            }
                        }

                        for (int n = 0; n < 3; n++)
                            InsertRow(ts, cols, rows, ref maxId, rng, ref seq);

                        foreach (var victim in rows.Values.Take(2).Select(r => r.Id).ToArray())
                        {
                            Assert.IsTrue(ts.Delete("t", victim).IsSuccess, "delete " + victim);
                            rows.Remove(victim);
                            dead.Add(victim);
                        }

                        if (cols.Count > 3)
                        {
                            var dropAt = 1 + (round % (cols.Count - 1));
                            var name = cols[dropAt].Name;
                            Assert.IsTrue(ts.DropColumn("t", name).IsSuccess, "drop " + name);
                            cols.RemoveAt(dropAt);
                            foreach (var row in rows.Values)
                                row.Values.Remove(name);
                        }

                        ts.SaveChanges();
                    }

                    Check(eng, cols, rows, dead, other, "round " + round);

                    if (backend == TestBackend.File && round % 3 == 2)
                    {
                        Reopen(ref eng, path!);
                        Check(eng, cols, rows, dead, other, "reopen " + round);
                    }

                    if (round % 3 == 2)
                    {
                        long before;
                        using (var ts = eng.StartTransaction())
                            before = ts.GetSpace().FileBytes;
                        eng.Vacuum();
                        eng.Vacuum();
                        using (var ts = eng.StartTransaction())
                        {
                            Assert.IsFalse(ts.GetSpace().HasFreePages, "vacuum left free pages at round " + round);
                            Assert.IsTrue(ts.GetSpace().FileBytes <= before, "vacuum grew the file at round " + round);
                        }

                        Check(eng, cols, rows, dead, other, "vacuum " + round);

                        using (var ts = eng.StartTransaction())
                        {
                            var previousMax = maxId;
                            InsertRow(ts, cols, rows, ref maxId, rng, ref seq);
                            Assert.IsTrue(maxId > previousMax, "row id was reused after vacuum");
                            ts.SaveChanges();
                        }
                    }
                }

                Check(eng, cols, rows, dead, other, "final");
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Head_column_churn_keeps_large_var_and_key(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                var payload = new string('测', 3500);
                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t",
                    [
                        ("kid", DbValueType.Int, true),
                        ("body", DbValueType.StrVar, false),
                    ]);
                    id = ts.Insert("t", [("kid", 1), ("body", payload)]).Value;
                    for (int i = 2; i <= 12; i++)
                        ts.Insert("t", [("kid", i), ("body", payload + i)]);
                    ts.SaveChanges();
                }

                for (int i = 0; i < 15; i++)
                {
                    using (var ts = eng.StartTransaction())
                    {
                        var name = "h" + i;
                        Assert.IsTrue(ts.AddColumn("t", name, i % 2 == 0 ? DbValueType.Int : DbValueType.Str32B, false, i % 2 == 0 ? (DbCell)(i + 3) : Fit32("H" + i), ordinal: 0).IsSuccess);
                        Assert.IsTrue(ts.Update("t", id, name, i % 2 == 0 ? (DbCell)(1000 + i) : Fit32("U" + i)).IsSuccess);
                        if (i > 0)
                            Assert.IsTrue(ts.DropColumn("t", "h" + (i - 1)).IsSuccess);
                        Assert.AreEqual(payload, ts.Find("t", "kid", 1).Row.GetString(ts.Find("t", id).Row.ColumnCount - 1));
                        ts.SaveChanges();
                    }

                    if (i % 4 == 3)
                    {
                        eng.Vacuum();
                        if (backend == TestBackend.File)
                            Reopen(ref eng, path!);
                    }
                }

                using var read = eng.StartTransaction();
                var row = read.Find("t", id);
                Assert.IsTrue(row.IsSuccess);
                Assert.AreEqual(1, row.Row.GetInt(1));
                Assert.AreEqual(payload, row.Row.GetString(2));
                Assert.AreEqual(1000 + 14, row.Row.GetInt(0));
                Assert.AreEqual(payload + 7, read.Find("t", "kid", 7).Row.GetString(2));
                Assert.ThrowsException<LumException>(() => read.Find("t", "h0", 3));
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Key_column_can_be_added_and_dropped_across_row_counts(TestBackend backend)
        {
            With(backend, eng =>
            {
                using var ts = eng.StartTransaction();
                ts.Create("t", [("kid", DbValueType.Int, true), ("name", DbValueType.Str32B, false)]);
                var id = ts.Insert("t", [("kid", 1), ("name", "one")]).Value;

                for (int i = 0; i < 8; i++)
                {
                    Assert.IsTrue(ts.AddColumn("t", "alt", DbValueType.Int, true, 100 + i).IsSuccess, "add key " + i);
                    Assert.AreEqual(1, ts.Find("t", "alt", 100 + i).Row.GetInt(0));
                    var second = ts.Insert("t", [("kid", 2 + i), ("name", "two"), ("alt", 500 + i)]);
                    Assert.IsTrue(second.IsSuccess, second.Exception?.Message);
                    Assert.IsTrue(ts.DropColumn("t", "alt").IsSuccess, "drop key " + i);
                    Assert.ThrowsException<LumException>(() => ts.Find("t", "alt", 100 + i));
                    Assert.AreEqual("one", ts.Find("t", id).Row.GetString(1));
                    Assert.AreEqual("two", ts.Find("t", "kid", 2 + i).Row.GetString(1));
                    Assert.IsTrue(ts.Delete("t", second.Value).IsSuccess);
                }

                ts.SaveChanges();
                ts.Dispose();
                eng.Vacuum();
                using var read = eng.StartTransaction();
                Assert.AreEqual("one", read.Find("t", "kid", 1).Row.GetString(1));
                Assert.IsFalse(read.Find("t", "kid", 2).IsSuccess);
            });
        }

        [TestMethod]
        [DynamicData(nameof(Backends))]
        public void Discard_rolls_back_uncommitted_alter_and_keeps_saved_rows(TestBackend backend)
        {
            With(backend, (eng, path) =>
            {
                uint id;
                using (var ts = eng.StartTransaction())
                {
                    ts.Create("t", [("kid", DbValueType.Int, true), ("name", DbValueType.Str32B, false)]);
                    id = ts.Insert("t", [("kid", 4), ("name", "saved")]).Value;
                    ts.SaveChanges();
                    Assert.IsTrue(ts.AddColumn("t", "temp", DbValueType.Int, false, 9).IsSuccess);
                    Assert.AreEqual(9, ts.Find("t", id).Row.GetInt(2));
                    ts.Discard();
                }

                if (backend == TestBackend.File)
                    Reopen(ref eng, path!);

                using var read = eng.StartTransaction();
                Assert.AreEqual(2, read.GetTableNames().Values.Single(v => v.tableName == "t").columns.Length);
                Assert.AreEqual("saved", read.Find("t", id).Row.GetString(1));
                Assert.ThrowsException<LumException>(() => read.Find("t", "temp", 9));
            });
        }

        private static void InsertRow(ITransaction ts, List<Col> cols, Dictionary<uint, Row> rows, ref uint maxId, Random rng, ref int seq)
        {
            var kid = seq + 1;
            var map = Build(cols, kid, seq, rng);
            seq++;
            var inserted = ts.Insert("t", cols.Select(c => (c.Name, ToCell(c, map[c.Name]))).ToArray());
            Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
            Assert.IsTrue(inserted.Value > maxId, "id went backwards");
            maxId = inserted.Value;
            rows[inserted.Value] = new Row { Id = inserted.Value, Values = map };
        }

        private static Dictionary<string, object> Build(List<Col> cols, int kid, int salt, Random rng)
        {
            var map = new Dictionary<string, object>();
            foreach (var col in cols)
                map[col.Name] = col.Name == "kid" ? kid : Make(col, kid, salt, rng);
            return map;
        }

        private static void Add(ITransaction ts, List<Col> cols, Dictionary<uint, Row> rows, Random rng, ref int seq, int ordinal)
        {
            var col = new Col
            {
                Name = "c" + seq,
                Kind = (Kind)rng.Next(0, 5),
                IsKey = false,
            };
            if (col.Kind == Kind.Int && cols.Count == 0)
                col.Kind = Kind.Str32;
            seq++;
            var sample = Make(col, kid: seq, salt: ordinal, rng);
            Assert.IsTrue(ts.AddColumn("t", col.Name, col.Type, false, ToCell(col, sample), ordinal).IsSuccess, col.Name);
            cols.Insert(ordinal, col);
            foreach (var row in rows.Values)
                row.Values[col.Name] = sample;
        }

        private static object Make(Col col, int kid, int salt, Random rng)
        {
            return col.Kind switch
            {
                Kind.Int => kid * 10 + salt,
                Kind.Bool => ((kid + salt) & 1) == 0,
                Kind.Str32 => Fit32($"s{kid}-{salt}"),
                Kind.StrVar => $"var-{kid}-{salt}-" + new string(salt % 2 == 0 ? '测' : 'Q', 1500 + (kid % 5) * 400),
                Kind.Bytes => new byte[] { (byte)kid, (byte)salt, (byte)rng.Next(1, 255), 7, 8, 9 },
                _ => throw new InvalidOperationException(),
            };
        }

        private static object FreshValue(Col col, Row row, int round, Random rng)
        {
            var tag = (int)row.Id;
            return col.Kind switch
            {
                Kind.Int => tag + round * 100,
                Kind.Bool => round % 2 == 0,
                Kind.Str32 => Fit32($"u{round}-{tag}"),
                Kind.StrVar => $"upd-{round}-{tag}-" + new string('改', 1800),
                Kind.Bytes => new byte[] { (byte)round, (byte)tag, 3 },
                _ => throw new InvalidOperationException(),
            };
        }

        private static DbCell ToCell(Col col, object value) => col.Kind switch
        {
            Kind.Int => (int)value,
            Kind.Bool => (bool)value,
            Kind.Str32 or Kind.StrVar => (string)value,
            Kind.Bytes => (byte[])value,
            _ => throw new InvalidOperationException(),
        };

        private static (string, DbValueType, bool)[] Schema(List<Col> cols)
            => cols.Select(c => (c.Name, c.Type, c.IsKey)).ToArray();

        private static string Fit32(string text) => text.Length <= 32 ? text : text[..32];

        private static void Check(DbEngine eng, List<Col> cols, Dictionary<uint, Row> rows, HashSet<uint> dead, Dictionary<uint, int> other, string where)
        {
            using var ts = eng.StartTransaction();
            var schema = ts.GetTableNames().Values.Single(v => v.tableName == "t").columns;
            Assert.AreEqual(cols.Count, schema.Length, where);
            for (int i = 0; i < cols.Count; i++)
            {
                Assert.AreEqual(cols[i].Name, schema[i].columnName, where);
                Assert.AreEqual(cols[i].IsKey, schema[i].isKey, where + " " + cols[i].Name);
                Assert.AreEqual(cols[i].Type.ToString(), schema[i].dataType, where + " " + cols[i].Name);
            }

            var kidOrdinal = cols.FindIndex(c => c.Name == "kid");
            var seen = new HashSet<uint>();
            ts.GoThrough("t", (uint id, ref RowView row) =>
            {
                Assert.IsTrue(rows.ContainsKey(id), where + " unexpected id " + id);
                Assert.IsTrue(seen.Add(id), where + " duplicate scan " + id);
                if (kidOrdinal >= 0)
                    Assert.AreEqual((int)rows[id].Values["kid"], row.GetInt(kidOrdinal), where);
                return true;
            });
            Assert.AreEqual(rows.Count, seen.Count, where + " scan count");

            foreach (var row in rows.Values)
            {
                var byId = ts.Find("t", row.Id);
                Assert.IsTrue(byId.IsSuccess, where + " missing id " + row.Id);
                AssertRow(byId.Row, cols, row, where);
                if (kidOrdinal >= 0)
                {
                    var kid = (int)row.Values["kid"];
                    var byKey = ts.Find("t", "kid", kid);
                    Assert.IsTrue(byKey.IsSuccess, where + " missing kid " + kid);
                    Assert.AreEqual(kid, byKey.Row.GetInt(kidOrdinal), where);
                }
            }

            foreach (var id in dead)
                Assert.IsFalse(ts.Find("t", id).IsSuccess, where + " deleted id still visible " + id);

            foreach (var pair in other)
            {
                Assert.IsTrue(ts.Find("other", pair.Key).IsSuccess, where + " other id " + pair.Key);
                var byKey = ts.Find("other", "k", pair.Value);
                Assert.IsTrue(byKey.IsSuccess, where + " other table lost " + pair.Value);
                Assert.AreEqual("other-" + new string('稳', 800) + pair.Value, byKey.Row.GetString(1));
            }
        }

        private static void AssertRow(IDbRow row, List<Col> cols, Row expected, string where)
        {
            Assert.AreEqual(cols.Count, row.ColumnCount, where);
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var value = expected.Values[col.Name];
                switch (col.Kind)
                {
                    case Kind.Int:
                        Assert.AreEqual((int)value, row.GetInt(i), where + " " + col.Name);
                        break;
                    case Kind.Bool:
                        Assert.AreEqual((bool)value, row.GetBool(i), where + " " + col.Name);
                        break;
                    case Kind.Str32:
                    case Kind.StrVar:
                        Assert.AreEqual((string)value, row.GetString(i), where + " " + col.Name);
                        break;
                    case Kind.Bytes:
                        CollectionAssert.AreEqual((byte[])value, row.GetBytes(i), where + " " + col.Name);
                        break;
                }
            }
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

        private static void Reopen(ref DbEngine eng, string path)
        {
            eng.Dispose();
            eng = new DbEngine(path);
        }
    }
}
