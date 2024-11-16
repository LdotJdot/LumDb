using System.Diagnostics;

namespace LumDbEngine.Utils.Test
{
    public static class MemTest
    {
        public static long GetMem()
        {
            Process proc = Process.GetCurrentProcess();
            long b = proc.PrivateMemorySize64;
            return b / 1024;
        }

        public static long GetGCTotalMemory(bool forceFullCollection)
        {
            long b = GC.GetTotalMemory(forceFullCollection);
            return b / 1024;
        }

        public static long GetThreadAllocate()
        {
            long b = GC.GetAllocatedBytesForCurrentThread();
            return b / 1024;
        }

        public struct MemChecker
        {
            private readonly long before;
            private readonly string tag;
            private readonly int threshold;

            public MemChecker(string tag, int threshold = 1)
            {
                this.before = MemTest.GetThreadAllocate();
                this.tag = tag;
                this.threshold = threshold;
            }

            private bool disposed = false;

            public void Dispose()
            {
                if (disposed == false)
                {
                    disposed = true;
                    var currMem = MemTest.GetThreadAllocate();
                    var incre = currMem - before;

                    if (incre > threshold)
                        Console.WriteLine($"tag:{tag}, gcmem increased:{incre} kb，currMem:{currMem} kb");
                }
            }
        }
    }
}