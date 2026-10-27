using System.Diagnostics;
using System.Text;
using LumDbEngine.Element.Exceptions;

namespace UnitTestLumDb.DefectHunt
{
    /// <summary>
    /// 证据固化工具：所有 DefectHunt 用例把现场观测（异常类型、耗时、数据状态）
    /// 追加写入工作区 TestEvidence/defect_hunt_evidence_yyyyMMdd.md。
    /// </summary>
    internal static class DhSupport
    {
        private static readonly object Gate = new();
        private static string? _root;

        public static string WorkspaceRoot()
        {
            if (_root != null) return _root;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LumDb.sln")))
                dir = dir.Parent;
            _root = dir?.FullName ?? AppContext.BaseDirectory;
            return _root;
        }

        public static string EvidencePath()
        {
            var dir = Path.Combine(WorkspaceRoot(), "TestEvidence");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"defect_hunt_evidence_{DateTime.Now:yyyyMMdd}.md");
        }

        public static void Evidence(string title, params (string key, object? value)[] fields)
        {
            lock (Gate)
            {
                var path = EvidencePath();
                var isNew = !File.Exists(path);
                using var w = new StreamWriter(path, append: true);
                if (isNew)
                {
                    w.WriteLine("# DefectHunt 现场证据");
                    w.WriteLine();
                    w.WriteLine($"- 机器: {Environment.MachineName}");
                    w.WriteLine($"- 系统: {Environment.OSVersion}");
                    w.WriteLine($"- 运行时: {Environment.Version}");
                    w.WriteLine($"- 采集开始: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    w.WriteLine();
                }
                w.WriteLine($"## {DateTime.Now:HH:mm:ss.fff} | {title}");
                foreach (var (k, v) in fields)
                    w.WriteLine($"- {k}: {v}");
                w.WriteLine();
                w.Flush();
            }
        }

        public static string TempPath(string tag)
        {
            var dir = Path.Combine(Path.GetTempPath(), "lumdb_defecthunt");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"{tag}_{Guid.NewGuid():N}");
        }

        public static void TryDelete(params string?[] paths)
        {
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                try { if (File.Exists(p)) File.Delete(p); } catch { }
                try { if (Directory.Exists(p)) Directory.Delete(p, true); } catch { }
            }
        }

        public static string? FindCrashProbe()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "CrashProbe.exe"),
                Path.Combine(AppContext.BaseDirectory, "CrashProbe"),
                Path.Combine(WorkspaceRoot(), "CrashProbe", "bin", "Debug", "net10.0", "CrashProbe.exe"),
                Path.Combine(WorkspaceRoot(), "CrashProbe", "bin", "Debug", "net10.0", "CrashProbe"),
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        public static string Describe(Exception? ex) =>
            ex == null ? "<null>" : $"{ex.GetType().FullName}: {ex.Message}";

        public static string Short(Exception? ex) =>
            ex == null ? "<null>" : $"{ex.GetType().Name}: {ex.Message}";

        /// <summary>Run an action and return the exception it threw (null when it succeeded).</summary>
        public static Exception? Capture(Action act)
        {
            try
            {
                act();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>True when the exception is the "transaction was aborted by a failed statement" signal.</summary>
        public static bool IsAborted(Exception? ex) =>
            ex is LumException && ex.Message.StartsWith(LumExceptionMessage.TransactionAborted, StringComparison.Ordinal);
    }
}