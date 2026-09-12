using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.PageIntegrity
{
    /// <summary>
    /// 回归「塌表」：数据页被回收后 RootDataPageId 被新页顶掉 / 悬空，
    /// 使行扫描从错误的页头开始，整表只剩最后一页可见。
    ///
    /// 触发链（缺一不可）：
    ///   1) 表已跨多个数据页，root 指向第一页；
    ///   2) 把「最后一页」上的行全部删空 → 该页 CurrentDataCount 归零，被 RecyclePage 回收；
    ///   3) 紧接着 Insert → RequestAvailableDataPage 走 InitializeNewDataPage 分配新页；
    ///   4) 修复前 InitializeNewDataPage 无条件 SetRootDataPageId(新页)，旧链整段成为孤儿页。
    ///
    /// 生产侧对应 OpenLum 的写法：<c>LumDbFile.Run</c>（每次操作 open/close 库）+ 同键「删后插」。
    /// 因此 <see cref="ReopenPerOperation_SameKeyDeleteThenInsert_AcrossPageBoundary_NoCollapse"/>
    /// 是复刻真实调用形态的核心回归用例。
    /// </summary>
    [TestClass]
    public class DataPageChainIntegrityTests
    {
        private const string T = "t";

        // Pk Long(8) + V Int(4) + S Str8B(8)：
        // 行载荷 20B，DataLength = 20 + 3*20 = 80，单页上限 = (4096-53)/80 = 50 行。
        // 注意：下文的「删一段后缀/中段」在容量上下浮动时依然成立，
        // 只要一次删除覆盖至少一整页，就必然触发页回收路径。
        private const int RowsPerPage = 50;

        private const int SeedRows = 300; // 6 页左右，足够覆盖多页链

        private static (string, DbCell)[] Row(long key, int value = 0)
            => [("Pk", key), ("V", value), ("S", "s" + key)];

        private static void CreateTable(string path)
        {
            using var eng = new DbEngine(path, true);
            using var ts = eng.StartTransaction();
            var r = ts.Create(T,
            [
                ("Pk", DbValueType.Long, true),
                ("V", DbValueType.Int, false),
                ("S", DbValueType.Str8B, false),
            ]);
            Assert.IsTrue(r.IsSuccess, "create table: " + r.Exception?.Message);
            ts.SaveChanges();
        }

        private static void Write(string path, Action<ITransaction> body)
        {
            using var eng = new DbEngine(path);
            eng.TimeoutMilliseconds = 30_000;
            using var ts = eng.StartTransaction();
            body(ts);
            ts.SaveChanges();
        }

        private static T Read<T>(string path, Func<ITransaction, T> body)
        {
            using var eng = new DbEngine(path);
            eng.TimeoutMilliseconds = 30_000;
            using var ts = eng.StartTransaction();
            return body(ts);
        }

        private static long[] Keys(ITransaction ts)
        {
            var keys = new List<long>();
            ts.GoThrough(T, (ref RowView r) =>
            {
                keys.Add(r.GetLong(0));
                return true;
            });
            return keys.ToArray();
        }

        private static void AssertKeysAre(string path, string scenario, params long[] expected)
        {
            var actual = Read(path, Keys);
            var exp = expected.OrderBy(k => k).ToArray();
            var got = actual.OrderBy(k => k).ToArray();
            CollectionAssert.AreEqual(exp, got,
                $"{scenario}：期望 {exp.Length} 行，实际 {got.Length} 行" +
                $"（缺失 Pk=[{string.Join(",", exp.Except(got).Take(12))}]" +
                (exp.Except(got).Count() > 12 ? "..." : "") + "）");
        }

        private static void InsertRange(ITransaction ts, long from, long to)
        {
            for (var k = from; k <= to; k++)
            {
                var r = ts.Insert(T, Row(k, (int)k));
                Assert.IsTrue(r.IsSuccess, $"insert Pk={k}: {r.Exception?.Message}");
            }
        }

        private static void DeleteRange(ITransaction ts, long from, long to)
        {
            for (var k = from; k <= to; k++)
                ts.Delete(T, "Pk", k);
        }

        private static void TryDelete(params string[] paths)
        {
            foreach (var p in paths)
            {
                try { if (File.Exists(p)) File.Delete(p); }
                catch { /* best effort */ }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 核心回归：复刻 OpenLum 的「每次操作重开库 + 同键删后插」。
        // 当键跨到新数据页后，同一轮的第二次「删后插」会把该新页删空并回收，
        // 紧接着的 Insert 触发新页分配 —— 正是 root 被顶掉的时刻。
        // 修复前：第 RowsPerPage+1 轮行数塌成 1。
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void ReopenPerOperation_SameKeyDeleteThenInsert_AcrossPageBoundary_NoCollapse()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                CreateTable(path);

                const int turns = 120; // 远超单页容量，确保一定跨越页边界

                for (long turn = 1; turn <= turns; turn++)
                {
                    for (var rep = 0; rep < 2; rep++)
                    {
                        var k = turn;
                        // 每次操作都 open/close 库，复刻 LumDbFile.Run 的用法。
                        Write(path, ts =>
                        {
                            ts.Delete(T, "Pk", k); // 首次该键不存在，容忍 no-op
                            var ins = ts.Insert(T, Row(k, (int)(1000 + turn)));
                            Assert.IsTrue(ins.IsSuccess, $"turn={turn} rep={rep} insert: {ins.Exception?.Message}");
                        });
                    }

                    var keys = Read(path, Keys);
                    Assert.AreEqual((int)turn, keys.Length,
                        $"第 {turn} 轮后只剩 {keys.Length} 行 → 数据页链塌陷（Pk=[{string.Join(",", keys.Take(8))}]）");
                }

                // 末态：键 1..120 全部存活且内容可读
                var finalKeys = Read(path, Keys);
                CollectionAssert.AreEqual(
                    Enumerable.Range(1, turns).Select(i => (long)i).ToArray(),
                    finalKeys.OrderBy(k => k).ToArray(),
                    "末态键集合不完整");
            }
            finally
            {
                TryDelete(path, path + ".log");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // root 页被删空：root 必须顺延到后继页，其余页不能被孤立。
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void EmptyRootPage_AdvancesRoot_RemainingRowsReachable()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                CreateTable(path);
                Write(path, ts => InsertRange(ts, 1, SeedRows));

                // 删掉最前面一整段（必然包含 root 页）
                Write(path, ts => DeleteRange(ts, 1, 3 * RowsPerPage));

                var survivors = Enumerable.Range(3 * RowsPerPage + 1, SeedRows - 3 * RowsPerPage)
                    .Select(i => (long)i).ToArray();
                AssertKeysAre(path, "删空 root 页后旧数据丢失", survivors);

                // 回收后继续插入，链必须仍然完整
                Write(path, ts => InsertRange(ts, 10_000, 10_002));
                AssertKeysAre(path, "root 顺延后再插入破坏了旧链",
                    [.. survivors, 10_000, 10_001, 10_002]);
            }
            finally
            {
                TryDelete(path, path + ".log");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 中间页被删空：前后页必须被重新链接，两侧数据都要可达。
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void EmptyInteriorPage_RelinksNeighbours_NoDataHidden()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                CreateTable(path);
                Write(path, ts => InsertRange(ts, 1, SeedRows));

                // 删空中间一段（必然包含至少一个整页）
                Write(path, ts => DeleteRange(ts, 2 * RowsPerPage + 1, 4 * RowsPerPage));

                var head = Enumerable.Range(1, 2 * RowsPerPage).Select(i => (long)i);
                var tail = Enumerable.Range(4 * RowsPerPage + 1, SeedRows - 4 * RowsPerPage).Select(i => (long)i);
                AssertKeysAre(path, "删空中间页后邻页数据不可达", [.. head, .. tail]);

                Write(path, ts => InsertRange(ts, 10_000, 10_000));
                AssertKeysAre(path, "中间页回收后再插入破坏了链",
                    [.. head, .. tail, 10_000]);
            }
            finally
            {
                TryDelete(path, path + ".log");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 尾页被删空 + 立即插入新行：新页必须接在链尾，不能变成新的 root。
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void EmptyLastPageThenInsert_MustAppendToTail_NotStealRoot()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                CreateTable(path);
                Write(path, ts => InsertRange(ts, 1, SeedRows));

                // 删空尾部一整段（必然包含最后一个数据页）
                Write(path, ts => DeleteRange(ts, SeedRows / 2 + 1, SeedRows));

                var survivors = Enumerable.Range(1, SeedRows / 2).Select(i => (long)i).ToArray();
                AssertKeysAre(path, "删空尾页后前半段丢失", survivors);

                // 此刻 AvailableDataPage 已被清空，插入必然走 InitializeNewDataPage
                Write(path, ts => InsertRange(ts, 900, 900));
                AssertKeysAre(path, "尾页回收后新页顶掉了 root", [.. survivors, 900]);
            }
            finally
            {
                TryDelete(path, path + ".log");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 种子化混合 fuzz：每次操作都重开库，随机 插 / 删 / 同键删后插。
        // 不变量：任意时刻可见行集合 == 逻辑存活集合（不丢行、不重复）。
        // ─────────────────────────────────────────────────────────────────────
        [TestMethod]
        public void ReopenPerOperation_SeededMixedOperations_NeverLosesRows()
        {
            var path = Configuration.GetRandomPath();
            try
            {
                CreateTable(path);

                var rng = new Random(20260912);
                var live = new HashSet<long>();
                var next = 1L;

                for (var op = 0; op < 300; op++)
                {
                    var roll = rng.Next(100);

                    if (roll < 45 || live.Count == 0)
                    {
                        var key = next++;
                        Write(path, ts =>
                        {
                            var r = ts.Insert(T, Row(key, (int)key));
                            Assert.IsTrue(r.IsSuccess, $"op={op} insert Pk={key}: {r.Exception?.Message}");
                        });
                        live.Add(key);
                    }
                    else if (roll < 70)
                    {
                        var key = live.ElementAt(rng.Next(live.Count));
                        Write(path, ts => ts.Delete(T, "Pk", key));
                        live.Remove(key);
                    }
                    else
                    {
                        var key = live.ElementAt(rng.Next(live.Count));
                        // 同键「删后插」——触发页回收 + 新页分配的关键形态
                        Write(path, ts =>
                        {
                            ts.Delete(T, "Pk", key);
                            var r = ts.Insert(T, Row(key, (int)key));
                            Assert.IsTrue(r.IsSuccess, $"op={op} reinsert Pk={key}: {r.Exception?.Message}");
                        });
                    }

                    var keys = Read(path, Keys);
                    Assert.AreEqual(live.Count, keys.Length,
                        $"op={op} 后可见 {keys.Length} 行 ≠ 逻辑存活 {live.Count} 行");
                }

                AssertKeysAre(path, "fuzz 末态键集合与期望不一致", [.. live]);
            }
            finally
            {
                TryDelete(path, path + ".log");
            }
        }
    }
}
