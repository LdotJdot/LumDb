using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;

namespace UnitTestLumDb.Config
{
    public enum TestBackend
    {
        Memory = 0,
        File = 1,
    }

    internal class Configuration
    {
        public static bool MemMode = true;

        public static string GetRandomPath()
        {
            return $"{Path.GetTempPath()}{Path.GetRandomFileName()}";
        }

        public static DbEngine GetDbEngineForTest()
        {
            return new DbEngine();
        }

        public static DbEngine GetDbEngineForTest(string path)
        {
            return new DbEngine(path);
        }

        public static DbEngine Create(TestBackend backend, out string? path)
        {
            if (backend == TestBackend.Memory)
            {
                path = null;
                return new DbEngine();
            }

            path = GetRandomPath();
            return new DbEngine(path);
        }

        /// <summary>
        /// File: close and reopen from disk. Memory: next transaction on the same engine.
        /// </summary>
        public static void VerifyCommitted(TestBackend backend, DbEngine live, string? path, Action<ITransaction> assert)
        {
            if (backend == TestBackend.Memory)
            {
                using var ts = live.StartTransaction();
                assert(ts);
                return;
            }

            live.Dispose();
            using var eng = new DbEngine(path!);
            using var ts2 = eng.StartTransaction();
            assert(ts2);
            eng.SetDestoryOnDisposed();
        }

        public static void Cleanup(TestBackend backend, DbEngine? eng, string? path)
        {
            try
            {
                if (backend == TestBackend.File)
                    eng?.SetDestoryOnDisposed();
            }
            catch
            {
                // already disposed
            }

            try { eng?.Dispose(); }
            catch { /* already disposed */ }

            if (backend == TestBackend.File && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }
    }
}
