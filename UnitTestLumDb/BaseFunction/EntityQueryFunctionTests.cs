using LumDbEngine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    [TestClass]
    [DoNotParallelize]
    public partial class EntityQueryFunctionTests
    {
        [LumEntity]
        public partial class FnItem
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            public int Score { get; set; }
            public double Val { get; set; }
            [Str32B] public string Name { get; set; } = "";
            [Str32B] public string Note { get; set; } = "";
        }

        [LumEntity]
        public partial class FnOrder
        {
            [Id] public uint Id { get; set; }
            public int UserCode { get; set; }
            [Str32B] public string Label { get; set; } = "";
            public int Qty { get; set; }
        }

        private static void Seed(ITransaction ts)
        {
            ts.Create<FnItem>("t");
            ts.Create<FnOrder>("orders");
            for (int i = 0; i < 20; i++)
            {
                var name = i switch
                {
                    0 => "  Alpha  ",
                    1 => "alpha",
                    2 => "BETA",
                    3 => "beta",
                    4 => "xx-yy",
                    _ => "n" + i,
                };
                ts.Insert("t", new FnItem
                {
                    Code = i,
                    Score = i < 5 ? -i * 10 : i * 10,
                    Val = i == 4 ? 9.0 : i + 0.6,
                    Name = name,
                    Note = i == 6 ? "   " : "tag" + i,
                });
            }

            ts.Insert("orders", new FnOrder { UserCode = 0, Label = "ALPHA", Qty = 2 });
            ts.Insert("orders", new FnOrder { UserCode = 0, Label = "other", Qty = 1 });
            ts.Insert("orders", new FnOrder { UserCode = 1, Label = "ALPHA", Qty = 5 });
            ts.Insert("orders", new FnOrder { UserCode = 3, Label = "beta", Qty = 9 });
        }

        [TestMethod]
        public void String_TrimToUpperStartsWith_AndToLower()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var upper = ts.Query<FnItem>("t")
                .Where(x => x.Name.Trim().ToUpper().StartsWith("AL"))
                .ToList()
                .Select(x => x.Code)
                .ToArray();
            CollectionAssert.AreEqual(new[] { 0, 1 }, upper);

            var lower = ts.Query<FnItem>("t")
                .Where(x => x.Name.Trim().ToLower() == "beta")
                .ToList()
                .Select(x => x.Code)
                .ToArray();
            CollectionAssert.AreEqual(new[] { 2, 3 }, lower);
        }

        [TestMethod]
        public void String_Substring_Replace_IndexOf_ContainsChar()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Substring(0, 2) == "xx").First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Replace("-", "/") == "xx/yy").First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.IndexOf("y") >= 0).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.LastIndexOf('y') == 4).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Contains('-')).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Replace('-', '/').StartsWith("xx/")).First().Code);
        }

        [TestMethod]
        public void String_IsNullOrWhiteSpace_ToString_Invariant()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Assert.AreEqual(6, ts.Query<FnItem>("t").Where(x => string.IsNullOrWhiteSpace(x.Note)).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Code.ToString() == "4").First().Code);
            Assert.AreEqual(0, ts.Query<FnItem>("t").Where(x => x.Name.ToUpperInvariant().Contains("ALPHA")).First().Code);
        }

        [TestMethod]
        public void Math_Abs_Min_Max_Clamp_Sign()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var abs30 = ts.Query<FnItem>("t").Where(x => Math.Abs(x.Score) == 30).ToList();
            Assert.AreEqual(1, abs30.Count);
            Assert.AreEqual(3, abs30[0].Code);

            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => Math.Sign(x.Score) < 0).Count());

            var clamped = ts.Query<FnItem>("t").Where(x => Math.Clamp(x.Score, 0, 100) == 0 && x.Code == 3).First();
            Assert.AreEqual(3, clamped.Code);

            Assert.AreEqual(5, ts.Query<FnItem>("t").Where(x => Math.Min(x.Score, x.Code) == 5).First().Code);
            Assert.AreEqual(19, ts.Query<FnItem>("t").Where(x => Math.Max(x.Score, x.Code) == 190).First().Code);
        }

        [TestMethod]
        public void Math_Floor_Ceiling_Round_Sqrt_Pow_Sin()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => Math.Floor(x.Val) == 9 && Math.Ceiling(x.Val) == 9).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => Math.Ceiling(x.Val) == 9 && x.Code == 4).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => Math.Round(x.Val) == 9 && x.Code == 4).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => Math.Sqrt(x.Val) == 3).First().Code);
            Assert.AreEqual(0, ts.Query<FnItem>("t").Where(x => x.Score < 0 && Math.Sqrt(x.Score) == 0).Count());
            Assert.AreEqual(2, ts.Query<FnItem>("t").Where(x => Math.Pow(x.Code, 2) == 4).First().Code);
            Assert.AreEqual(0, ts.Query<FnItem>("t").Where(x => x.Code == 0 && Math.Abs(Math.Sin(x.Val - 0.6)) < 0.0000001).First().Code);
        }

        [TestMethod]
        public void Complex_NestedBoolean_Functions_SkipTake()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var rows = ts.Query<FnItem>("t")
                .Where(x =>
                    (x.Name.Trim().ToUpper().StartsWith("AL") || x.Name.ToLower() == "beta")
                    && Math.Abs(x.Score) >= 10
                    && !string.IsNullOrWhiteSpace(x.Note)
                    && x.Name.Length > 2)
                .OrderByDescending(x => x.Code)
                .Skip(1)
                .Take(2)
                .ToList();

            CollectionAssert.AreEqual(new[] { 2, 1 }, rows.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void Join_ToUpperEqui_AndMathResidual()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var pairs = ts.Query<FnItem>("t")
                .Join<FnOrder>("orders", (u, o) => u.Code == o.UserCode && u.Name.Trim().ToUpper() == o.Label.ToUpper())
                .Where((u, o) => Math.Abs(u.Score) < 50 && o.Qty >= 2 && o.Qty <= 5)
                .OrderBy((u, o) => o.Qty)
                .ToList();

            Assert.AreEqual(2, pairs.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, pairs.Select(p => p.Outer.Code).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 5 }, pairs.Select(p => p.Inner.Qty).ToArray());
        }

        [TestMethod]
        public void WhereExists_MathAndString()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var users = ts.Query<FnItem>("t")
                .WhereExists<FnOrder>("orders", (u, o) =>
                    u.Code == o.UserCode
                    && u.Name.Trim().ToLower() == o.Label.ToLower()
                    && Math.Max(o.Qty, 1) >= 5)
                .ToList();

            CollectionAssert.AreEqual(new[] { 1, 3 }, users.Select(u => u.Code).ToArray());
        }

        [TestMethod]
        public void OrderBy_ToLower_ThenTake()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var first = ts.Query<FnItem>("t")
                .Where(x => x.Code <= 3)
                .OrderBy(x => x.Name.Trim().ToLower())
                .ToList();

            Assert.AreEqual(4, first.Count);
            Assert.AreEqual("alpha", first[0].Name.Trim().ToLower());
            Assert.AreEqual("alpha", first[1].Name.Trim().ToLower());
            Assert.AreEqual("beta", first[2].Name.Trim().ToLower());
            Assert.AreEqual("beta", first[3].Name.Trim().ToLower());
        }

        [TestMethod]
        public void Unsupported_CultureAndSplit_StillThrow()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            try
            {
                ts.Query<FnItem>("t").Where(x => x.Name.ToUpper(System.Globalization.CultureInfo.InvariantCulture) == "A").ToList();
                Assert.Fail("expected LumException");
            }
            catch (LumException)
            {
            }

            try
            {
                ts.Query<FnItem>("t").Where(x => x.Name.Split('-').Length > 1).ToList();
                Assert.Fail("expected LumException");
            }
            catch (LumException)
            {
            }
        }

        [TestMethod]
        public void CapturedFunc_Regex_OnColumn()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Func<string, bool> isXx = s => System.Text.RegularExpressions.Regex.IsMatch(s, "^xx");
            var hit = ts.Query<FnItem>("t").Where(x => isXx(x.Name)).ToList();
            Assert.AreEqual(1, hit.Count);
            Assert.AreEqual(4, hit[0].Code);
        }

        [TestMethod]
        public void Where_EntityCallback_AndRowView()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var byEntity = ts.Query<FnItem>("t")
                .WhereCallback(x => System.Text.RegularExpressions.Regex.IsMatch(x.Name.Trim(), "alpha", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .ToList();
            Assert.AreEqual(2, byEntity.Count);

            var byRow = ts.Query<FnItem>("t")
                .Where((ref RowView row) => row.GetInt(0) == 4)
                .ToList();
            Assert.AreEqual(1, byRow.Count);
            Assert.AreEqual("xx-yy", byRow[0].Name);
        }

        private static int Add(int a, int b) => a + b;

        private static string FoldName(string s) => s.Trim().ToUpperInvariant();

        private static bool InRange(int v, int lo, int hi) => v >= lo && v <= hi;

        [TestMethod]
        public void PlainCsharpMethods_NoFuncWrapper()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => Add(x.Code, x.Score) == 0).Count());
            CollectionAssert.AreEqual(
                new[] { 0, 1 },
                ts.Query<FnItem>("t").Where(x => FoldName(x.Name) == "ALPHA").ToList().Select(x => x.Code).ToArray());
            Assert.IsTrue(ts.Query<FnItem>("t").Where(x => InRange(x.Score, 40, 80)).Count() > 0);
            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => Add(x.Code, x.Code) == 8).Count());
        }

        private static T Echo<T>(T v) => v!;

        private static T Max2<T>(T a, T b) where T : IComparable<T>
            => Comparer<T>.Default.Compare(a, b) >= 0 ? a : b;

        private static TOut Combine<T1, T2, TOut>(T1 a, T2 b, Func<T1, T2, TOut> f) => f(a, b);

        [TestMethod]
        public void CapturedFunc_GenericShapes_AsScalar()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Func<string, string> fold = static s => s.Trim().ToUpperInvariant();
            var folded = ts.Query<FnItem>("t").Where(x => fold(x.Name) == "ALPHA").ToList();
            CollectionAssert.AreEqual(new[] { 0, 1 }, folded.Select(x => x.Code).ToArray());

            Func<int, int> twice = static n => n * 2;
            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => twice(x.Code) == 8).Count());

            Func<int, int, int> add = static (a, b) => a + b;
            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => add(x.Code, x.Score) == 0).Count());

            Func<int, int, int, int> clamp = static (v, lo, hi) => v < lo ? lo : (v > hi ? hi : v);
            var clamped = ts.Query<FnItem>("t").Where(x => clamp(x.Score, 0, 50) == 50).ToList();
            Assert.IsTrue(clamped.Count > 0);
            Assert.IsTrue(clamped.All(x => x.Score >= 50));

            var ordered = ts.Query<FnItem>("t").OrderByDescending(x => twice(x.Code)).Take(2).ToList();
            CollectionAssert.AreEqual(new[] { 19, 18 }, ordered.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void GenericHelperMethod_ClosedByCompiler()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var echo = ts.Query<FnItem>("t").Where(x => Echo(x.Name) == "alpha").ToList();
            Assert.AreEqual(1, echo.Count);
            Assert.AreEqual(1, echo[0].Code);

            Assert.AreEqual(
                ts.Query<FnItem>("t").Where(x => x.Score >= 40).Count(),
                ts.Query<FnItem>("t").Where(x => Max2(x.Score, 40) == x.Score && x.Score >= 40).Count());

            Func<int, string, string> pair = static (n, s) => n + ":" + s.Trim();
            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => Combine(x.Code, x.Name, pair) == "1:alpha").Count());
        }

        private static string Prefix2(string s) => s.Length >= 2 ? s.Substring(0, 2) : s;

        [TestMethod]
        public void Nested_UserMethod_AndStringContains()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            CollectionAssert.AreEqual(
                new[] { 0, 1 },
                ts.Query<FnItem>("t").Where(x => FoldName(x.Name).Contains("AL")).ToList().Select(x => x.Code).ToArray());

            CollectionAssert.AreEqual(
                new[] { 0, 1 },
                ts.Query<FnItem>("t").Where(x => Echo(FoldName(x.Name)).StartsWith("AL")).ToList().Select(x => x.Code).ToArray());

            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => Add(Add(x.Code, 1), x.Score) == 1).Count());
            Assert.AreEqual(3, ts.Query<FnItem>("t").Where(x => Math.Abs(Add(x.Score, 0)) == 30).First().Code);
            Assert.AreEqual(5, ts.Query<FnItem>("t").Where(x => Math.Max(Add(x.Code, 0), x.Score) == 50 && x.Code == 5).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => InRange(Math.Abs(x.Score), 35, 45) && x.Code == 4).First().Code);

            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Contains("xx") && x.Name.Contains('-')).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Trim().ToLower().Contains("xx")).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Name.Contains(Prefix2(x.Name)) && x.Code == 4).First().Code);
            Assert.AreEqual(0, ts.Query<FnItem>("t").Where(x => x.Name.ToUpper().EndsWith("PHA  ") && x.Code == 0).First().Code);
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => x.Code == 4 && x.Name.Substring(x.Name.IndexOf("x"), 2) == "xx").First().Code);
        }

        [TestMethod]
        public void Nested_CapturedFunc_ThenStringOp()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            Func<string, string> fold = static s => s.Trim().ToUpperInvariant();
            Func<int, int> twice = static n => n * 2;
            CollectionAssert.AreEqual(
                new[] { 0, 1 },
                ts.Query<FnItem>("t").Where(x => fold(x.Name).Contains("ALPH")).ToList().Select(x => x.Code).ToArray());
            Assert.AreEqual(1, ts.Query<FnItem>("t").Where(x => twice(Add(x.Code, 0)) == 8).Count());
            Assert.AreEqual(4, ts.Query<FnItem>("t").Where(x => fold(x.Name.Replace("-", "/")).Contains("XX/")).First().Code);

            var page = ts.Query<FnItem>("t")
                .Where(x => x.Code >= 10)
                .OrderByDescending(x => twice(Add(x.Code, 0)))
                .Take(3)
                .ToList();
            CollectionAssert.AreEqual(new[] { 19, 18, 17 }, page.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void Nested_JoinAndExists_Contains()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            var pairs = ts.Query<FnItem>("t")
                .Join<FnOrder>("orders", (u, o) =>
                    u.Code == o.UserCode
                    && FoldName(u.Name).Contains(Prefix2(o.Label.ToUpper())))
                .Where((u, o) => o.Qty >= 2)
                .ToList();
            Assert.IsTrue(pairs.Count >= 1);
            Assert.IsTrue(pairs.All(p => p.Outer.Code == p.Inner.UserCode));
            Assert.IsTrue(pairs.All(p => FoldName(p.Outer.Name).Contains(Prefix2(p.Inner.Label.ToUpper()))));

            var exists = ts.Query<FnItem>("t")
                .WhereExists<FnOrder>("orders", (u, o) =>
                    u.Code == o.UserCode && o.Label.ToUpper().Contains("ALP") && Math.Max(o.Qty, 0) >= 2)
                .ToList();
            CollectionAssert.AreEqual(new[] { 0, 1 }, exists.Select(u => u.Code).ToArray());
        }

        [TestMethod]
        public void Nested_UnsupportedOverloads_StillThrow()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Seed(ts);

            try
            {
                ts.Query<FnItem>("t").Where(x => x.Name.Contains("al", StringComparison.OrdinalIgnoreCase)).ToList();
                Assert.Fail("Contains(StringComparison) must not translate");
            }
            catch (LumException)
            {
            }

            try
            {
                ts.Query<FnItem>("t").Where(x => x.Name.StartsWith("AL", StringComparison.Ordinal)).ToList();
                Assert.Fail("StartsWith(StringComparison) must not translate");
            }
            catch (LumException)
            {
            }
        }
    }
}
