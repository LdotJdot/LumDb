using System.Diagnostics;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;

namespace UnitTestLumDb.DefectHunt
{
    /// <summary>
    /// DH7 崩溃一致性矩阵：子进程（CrashProbe）持续“单行事务 + SaveChanges”，
    /// 父进程在不同时刻强杀进程树，然后重开数据库验证：
    ///   1) 引擎必须能打开（WAL 回放或旧镜像）；
    ///   2) 行必须是连续前缀，载荷逐行一致，无重复、无空洞；
    ///   3) 已提交行数只能是“最后打印的已提交序号 +1 / +2”（回放窗口）。
    /// 任何其它形态都说明崩溃会写坏数据。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DefectHunt_CrashDurabilityTests
    {
        private const string Table = "crash";

        private static string Payload(int k) => new string((char)('a' + k % 26), 80) + ":" + k;

        [TestMethod]
        public void DH7_CrashMatrix_KillDuringCommits_ReopenMustBeConsistent()
        {
            var probe = DhSupport.FindCrashProbe();
            if (probe == null)
                Assert.Inconclusive("CrashProbe 未构建。先执行: dotnet build CrashProbe/CrashProbe.csproj -c Debug");

            int[] delays = { 200, 500, 900, 1500, 2200 };
            var failures = new List<string>();
            var report = new List<string>();

            foreach (var delay in delays)
            {
                var path = DhSupport.TempPath($"dh7_{delay}");
                try
                {
                    var psi = new ProcessStartInfo(probe)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    psi.ArgumentList.Add(path);
                    psi.ArgumentList.Add("60000");

                    using var p = Process.Start(psi)!;
                    var stdoutTask = p.StandardOutput.ReadToEndAsync();
                    var stderrTask = p.StandardError.ReadToEndAsync();

                    Thread.Sleep(delay);
                    try { p.Kill(entireProcessTree: true); } catch { }
                    p.WaitForExit(10000);

                    var stdout = stdoutTask.GetAwaiter().GetResult();
                    var stderr = stderrTask.GetAwaiter().GetResult();

                    int lastCommitted = -1;
                    foreach (var line in stdout.Split('\n'))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("C ", StringComparison.Ordinal) &&
                            int.TryParse(t.AsSpan(2), out var v))
                            lastCommitted = Math.Max(lastCommitted, v);
                    }

                    int rows = -1;
                    bool payloadOk = false;
                    string? verifyError = null;
                    try
                    {
                        using var eng = new DbEngine(path);
                        using var ts = eng.StartTransaction();
                        rows = 0;
                        payloadOk = true;
                        while (true)
                        {
                            var r = ts.Find(Table, "k", rows);
                            if (!r.IsSuccess) break;
                            if (r.Row.GetString(1) != Payload(rows))
                            {
                                payloadOk = false;
                                verifyError = $"第 {rows} 行载荷不一致";
                                break;
                            }
                            rows++;
                        }

                        int total = 0;
                        ts.GoThrough(Table, (ref RowView _) => { total++; return true; });
                        if (verifyError == null && total != rows)
                            verifyError = $"扫描总数 {total} 与连续前缀 {rows} 不一致（重复或空洞）";
                    }
                    catch (Exception ex)
                    {
                        verifyError = "重开失败: " + DhSupport.Short(ex);
                        rows = -1;
                    }

                    int lo = Math.Max(0, lastCommitted + 1);
                    int hi = Math.Max(0, lastCommitted + 2);
                    bool ok = verifyError == null && payloadOk && rows >= lo && rows <= hi;
                    var lineReport =
                        $"delay={delay}ms lastCommitted={lastCommitted} rows={rows} expected=[{lo},{hi}] " +
                        $"payloadOk={payloadOk} err={verifyError ?? "-"}";
                    report.Add(lineReport);

                    DhSupport.Evidence($"DH7 崩溃矩阵 {delay}ms",
                        ("结果", ok ? "OK" : "FAIL"),
                        ("lastCommitted", lastCommitted),
                        ("重开后连续行数", rows),
                        ("允许区间", $"[{lo},{hi}]"),
                        ("载荷校验", payloadOk),
                        ("错误", verifyError ?? "-"),
                        ("stdout 尾部", Tail(stdout, 120)),
                        ("stderr", Tail(stderr, 200)));

                    if (!ok) failures.Add(lineReport);
                }
                finally
                {
                    DhSupport.TryDelete(path, path + ".log",
                        path + ".vacuum.tmp", path + ".vacuum.snap", path + ".vacuum.bak");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "[DEFECT] 崩溃后数据不一致:\n" + string.Join("\n", failures) +
                "\n全部结果:\n" + string.Join("\n", report));
        }

        private static string Tail(string s, int max)
        {
            s = s.Replace("\r", "").Trim();
            return s.Length <= max ? s : s[^max..];
        }
    }
}