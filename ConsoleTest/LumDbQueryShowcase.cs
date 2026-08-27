using System.Diagnostics;
using System.Text;
using LumDbEngine;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace ConsoleTest
{
    /// <summary>
    /// LumDb 2.2.0 Query / Join / WhereExists / Find_Entity 表达式 API 演示。
    /// </summary>
    internal static partial class LumDbQueryShowcase
    {
        const string TIssue = "demo_issue";
        const string TComment = "demo_comment";
        const string THand = "demo_hand";

        public static void Run(bool quick = false)
        {
            Console.OutputEncoding = Encoding.UTF8;
            int issueCount = quick ? 200 : 800;
            int commentCount = quick ? 500 : 2400;

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  LumDb 2.2.0 · Query API 案例集");
            Console.WriteLine($"  规模 Issue={issueCount}  Comment={commentCount}  库=内存");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            using var eng = new DbEngine();
            Seed(eng, issueCount, commentCount);

            Section("1. Where + Count（RowView 扫描，不 materialize 全表）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                int open = ts.Query<DemoIssue>(TIssue).Where(i => i.State == 0).Count();
                int review = ts.Query<DemoIssue>(TIssue).Where(i => i.State == 2).Count();
                int closed = ts.Query<DemoIssue>(TIssue).Where(i => i.State == 3).Count();
                Console.WriteLine($"  Open={open}  Review={review}  Closed={closed}  合计={open + review + closed + CountOther(ts)}");
            });

            Section("2. 反向分页（PNWebHost QueryIssues 同款：predicate + skip + limit + isBackward）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                const int pageSize = 20;
                for (int page = 0; page < 3; page++)
                {
                    var res = ts.Find<DemoIssue>(
                        TIssue,
                        i => i.State == 0,
                        skip: (uint)(pageSize * page),
                        limit: (uint)pageSize,
                        isBackward: true);
                    var nos = string.Join(", ", res.Values.Select(x => x.IssueNo).Take(5));
                    Console.WriteLine($"  page {page}: {res.Values.Count} 条  首屏 IssueNo=[{nos}{(res.Values.Count > 5 ? ", …" : "")}]");
                }
            });

            Section("3. Query 链式：Where → Reverse → Skip → Take", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                var page = ts.Query<DemoIssue>(TIssue)
                    .Where(i => i.State == 0)
                    .Reverse()
                    .Skip(40)
                    .Take(15)
                    .ToList();
                Console.WriteLine($"  Open 状态第 3 页(15条): [{string.Join(", ", page.Select(x => x.IssueNo))}]");
            });

            Section("4. 嵌套字符串（Trim → ToUpper → Contains）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                var hits = ts.Query<DemoIssue>(TIssue)
                    .Where(i => i.Title.Trim().ToUpper().Contains("BUG"))
                    .Take(8)
                    .ToList();
                foreach (var h in hits)
                    Console.WriteLine($"  #{h.IssueNo}  {h.Title}");
            });

            Section("5. 用户静态方法 + 捕获 Func", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                Func<int, bool> active = n => n == 0 || n == 1 || n == 2;
                var rows = ts.Query<DemoIssue>(TIssue)
                    .Where(i => i.AuthorId >= 5 && active(i.State))
                    .Take(6)
                    .ToList();
                Console.WriteLine($"  AuthorId≥5 且活跃状态: {rows.Count} 条  IssueNo={string.Join(",", rows.Select(r => r.IssueNo))}");
            });

            Section("6. Join（Issue ⨝ Comment，等值连接）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                var pairs = ts.Query<DemoIssue>(TIssue)
                    .Join<DemoComment>(TComment, (i, c) => i.Id == c.IssueId && c.State == 0)
                    .Where((i, c) => i.State == 0)
                    .Take(5)
                    .ToList();
                foreach (var (issue, comment) in pairs)
                    Console.WriteLine($"  Issue#{issue.IssueNo} ← \"{Trim(comment.Body, 48)}\"");
            });

            Section("7. WhereExists（有未删评论的 Open Issue）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                int n = ts.Query<DemoIssue>(TIssue)
                    .Where(i => i.State == 0)
                    .WhereExists<DemoComment>(TComment, (i, c) => c.IssueId == i.Id && c.State == 0)
                    .Count();
                var sample = ts.Query<DemoIssue>(TIssue)
                    .Where(i => i.State == 0)
                    .WhereExists<DemoComment>(TComment, (i, c) => c.IssueId == i.Id && c.State == 0)
                    .Take(4)
                    .ToList();
                Console.WriteLine($"  命中 {n} 条 Open Issue（含评论）  样例 IssueNo=[{string.Join(", ", sample.Select(x => x.IssueNo))}]");
            });

            Section("8. IDbEntity · Find_Entity 表达式 + Query_Entity 链", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                var byFind = ts.Find_Entity<HandRow>(THand, r => r.Score >= 80 && r.Region == 1, limit: 5);
                var byQuery = ts.Query_Entity<HandRow>(THand)
                    .Where(r => r.Score >= 80 && r.Region == 1)
                    .Take(5)
                    .ToValues();
                Console.WriteLine($"  Find_Entity  Score≥80 Region=1 → {byFind.Values.Count} 条");
                Console.WriteLine($"  Query_Entity 同上 → {byQuery.Values.Count} 条  Code=[{string.Join(",", byQuery.Values.Select(v => v.Code))}]");
            });

            Section("9. OrderByDescending + Take（Join 后排序窗口）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                var top = ts.Query<DemoIssue>(TIssue)
                    .Where(i => i.State <= 2)
                    .OrderByDescending(i => i.IssueNo)
                    .Take(6)
                    .ToList();
                Console.WriteLine($"  最新 6 条活跃 Issue: [{string.Join(", ", top.Select(x => x.IssueNo))}]");
            });

            Section("10. 条件 Delete（清理已删评论 State=9）", () =>
            {
                using (var ts = eng.StartTransaction())
                {
                    int before = ts.Query<DemoComment>(TComment).Where(c => c.State == 9).Count();
                    int deleted = ts.Query<DemoComment>(TComment).Where(c => c.State == 9).Delete();
                    int after = ts.Query<DemoComment>(TComment).Where(c => c.State == 9).Count();
                    Console.WriteLine($"  删除前={before}  已删={deleted}  剩余={after}");
                }
            });

            Section($"11. 性能快照（Issue={issueCount} 行 · 单次 readonly 事务）", () =>
            {
                using var ts = eng.StartTransactionReadonly();
                var t0 = Stopwatch.GetTimestamp();
                int c = ts.Query<DemoIssue>(TIssue).Where(i => i.State == 0).Count();
                long countTicks = Stopwatch.GetTimestamp() - t0;

                t0 = Stopwatch.GetTimestamp();
                var all = ts.Find<DemoIssue>(TIssue, _ => true);
                long findAllTicks = Stopwatch.GetTimestamp() - t0;

                int deepSkip = Math.Max(0, issueCount - 40);
                t0 = Stopwatch.GetTimestamp();
                var page = ts.Find<DemoIssue>(TIssue, i => i.State == 0, skip: (uint)deepSkip, limit: 20, isBackward: true);
                long pageTicks = Stopwatch.GetTimestamp() - t0;

                double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
                Console.WriteLine($"  Count(State=0)={c}          {Ms(countTicks),8:N2} ms");
                Console.WriteLine($"  Find 全表 materialize       {Ms(findAllTicks),8:N2} ms  ({all.Values.Count} 实体)");
                Console.WriteLine($"  Find 反向深分页({deepSkip}+20)     {Ms(pageTicks),8:N2} ms  ({page.Values.Count} 实体)");
                if (pageTicks > 0 && findAllTicks > pageTicks)
                    Console.WriteLine($"  深分页相对全表 materialize ≈ {findAllTicks / (double)pageTicks:N0}× 更快");
            });

            Console.WriteLine();
            Console.WriteLine("  Query 案例集完成。");
            Console.WriteLine();
        }

        static int CountOther(ITransactionReadonly ts)
        {
            return ts.Query<DemoIssue>(TIssue).Count() -
                   ts.Query<DemoIssue>(TIssue).Where(i => i.State == 0 || i.State == 2 || i.State == 3).Count();
        }

        static void Section(string title, Action body)
        {
            Console.WriteLine();
            Console.WriteLine($"── {title}");
            var sw = Stopwatch.StartNew();
            body();
            sw.Stop();
            Console.WriteLine($"   ({sw.Elapsed.TotalMilliseconds:N1} ms)");
        }

        static void Seed(DbEngine eng, int issues, int comments)
        {
            using (var ts = eng.StartTransaction())
            {
                ts.Create<DemoIssue>(TIssue);
                ts.Create<DemoComment>(TComment);
                ts.Create(THand, [
                    ("Code", DbValueType.Int, true),
                    ("Score", DbValueType.Int, false),
                    ("Region", DbValueType.Int, false),
                ]);
            }

            var rng = new Random(20260827);
            uint[] issueIds = new uint[issues + 1];

            using (var ts = eng.StartTransaction())
            {
                for (int i = 1; i <= issues; i++)
                {
                    int state = i % 7 switch { 0 => 3, 1 => 0, 2 => 1, 3 => 2, 4 => 0, 5 => 4, _ => 1 };
                    issueIds[i] = ts.Insert(TIssue, new DemoIssue
                    {
                        IssueNo = 10_000 + i,
                        State = state,
                        AuthorId = 1 + i % 24,
                        Title = i % 5 == 0 ? $" BUG-{i} regression " : $"Task-{i}",
                        CreatedUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i),
                    }).Value;
                }

                for (int j = 0; j < comments; j++)
                {
                    int idx = 1 + rng.Next(issues);
                    int st = rng.Next(20) == 0 ? 9 : 0;
                    ts.Insert(TComment, new DemoComment
                    {
                        IssueId = issueIds[idx],
                        State = st,
                        Body = Lorem(rng, 20, 120),
                    });
                }

                for (int k = 0; k < 120; k++)
                {
                    ts.Insert_Entity(THand, new HandRow
                    {
                        Code = 1000 + k,
                        Score = 40 + rng.Next(70),
                        Region = k % 4,
                    });
                }
            }
        }

        static string Trim(string s, int max) =>
            s.Length <= max ? s : s[..max] + "…";

        static string Lorem(Random rng, int min, int max)
        {
            int n = min + rng.Next(Math.Max(1, max - min));
            const string abc = "abcdefghijklmnopqrstuvwxyz ";
            Span<char> buf = stackalloc char[n];
            for (int i = 0; i < n; i++) buf[i] = abc[rng.Next(abc.Length)];
            return new string(buf);
        }

        [LumEntity]
        public partial class DemoIssue
        {
            [Id] public uint Id { get; set; }
            [Key] public int IssueNo { get; set; }
            public int State { get; set; }
            public int AuthorId { get; set; }
            [Str32B] public string Title { get; set; } = "";
            public DateTime CreatedUtc { get; set; }
        }

        [LumEntity]
        public partial class DemoComment
        {
            [Id] public uint Id { get; set; }
            public uint IssueId { get; set; }
            public int State { get; set; }
            [StrVar] public string Body { get; set; } = "";
        }

        public sealed class HandRow : IDbEntity
        {
            public uint Id { get; set; }
            public int Code { get; set; }
            public int Score { get; set; }
            public int Region { get; set; }

            public void WriteTo(ref RowWriter writer)
            {
                writer.WriteInt(Code);
                writer.WriteInt(Score);
                writer.WriteInt(Region);
            }

            public bool TryReadFrom(IDbRow row)
            {
                Code = row.GetInt(0);
                Score = row.GetInt(1);
                Region = row.GetInt(2);
                return true;
            }

            public void GetId(uint id) => Id = id;
        }
    }
}
