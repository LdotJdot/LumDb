using System.Diagnostics;

namespace LumDbEngine.Element.Engine.Lock
{
    /// <summary>
    /// Thread-agnostic reader/writer gate.
    ///
    /// A lease is represented by counters/flags on the gate instance instead of thread ownership,
    /// so a lease acquired on one thread can be released on another. This is required because a
    /// transaction may be disposed on a different thread than the one that created it
    /// (await continuations, thread-pool work, DI scopes). ReaderWriterLockSlim cannot do this:
    /// its Exit* methods throw when called from a foreign thread and the lock cannot be
    /// force-released, which used to brick the engine permanently.
    ///
    /// Semantics kept from ReaderWriterLockSlim:
    /// - many concurrent readers, a writer excludes readers and vice versa;
    /// - only one upgradeable holder at a time, and it coexists with plain readers;
    /// - the upgradeable holder can upgrade to a writer;
    /// - writers are not starved by a stream of readers (writer preference);
    /// - same-thread reentrancy for write/upgradeable leases.
    /// </summary>
    internal sealed class EngineGate : IDisposable
    {
        private readonly object sync = new object();

        private int readers;

        private bool writer;
        private int writerThreadId = -1;
        private int writeDepth;

        private bool upgradeable;
        private int upgradeableThreadId = -1;
        private int upgradeableDepth;

        private bool writerWaiting;
        private bool disposed;

        /// <summary>True when the calling thread holds an upgradeable lease on this gate.</summary>
        internal bool HoldsUpgradeableOnCurrentThread
        {
            get
            {
                lock (sync)
                {
                    return upgradeable && upgradeableThreadId == Environment.CurrentManagedThreadId;
                }
            }
        }

        /// <summary>True when the calling thread holds the write lease on this gate.</summary>
        internal bool HoldsWriterOnCurrentThread
        {
            get
            {
                lock (sync)
                {
                    return writer && writerThreadId == Environment.CurrentManagedThreadId;
                }
            }
        }

        internal bool EnterRead(int timeoutMilliseconds)
        {
            lock (sync)
            {
                if (disposed) return false;
                if (!WaitWhile(() => writer || writerWaiting, timeoutMilliseconds)) return false;
                readers++;
                return true;
            }
        }

        internal void ExitRead()
        {
            lock (sync)
            {
                if (readers > 0) readers--;
                Monitor.PulseAll(sync);
            }
        }

        internal bool EnterUpgradeable(int timeoutMilliseconds)
        {
            lock (sync)
            {
                if (disposed) return false;

                if (upgradeable && upgradeableThreadId == Environment.CurrentManagedThreadId)
                {
                    upgradeableDepth++;
                    return true;
                }

                if (!WaitWhile(() => writer || upgradeable || writerWaiting, timeoutMilliseconds)) return false;

                upgradeable = true;
                upgradeableThreadId = Environment.CurrentManagedThreadId;
                upgradeableDepth = 1;
                return true;
            }
        }

        internal void ExitUpgradeable()
        {
            lock (sync)
            {
                if (!upgradeable) return;
                if (--upgradeableDepth > 0) return;

                upgradeable = false;
                upgradeableThreadId = -1;
                upgradeableDepth = 0;
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>
        /// Acquire the write lease. Waits for readers to drain and for a foreign upgradeable
        /// holder to leave; a same-thread upgradeable holder may enter (legacy recursion).
        /// </summary>
        internal bool EnterWrite(int timeoutMilliseconds)
        {
            lock (sync)
            {
                if (disposed) return false;

                if (writer && writerThreadId == Environment.CurrentManagedThreadId)
                {
                    writeDepth++;
                    return true;
                }

                bool selfUpgradeable = upgradeable && upgradeableThreadId == Environment.CurrentManagedThreadId;
                writerWaiting = true;
                try
                {
                    if (!WaitWhile(() => writer || readers > 0 || (upgradeable && !selfUpgradeable), timeoutMilliseconds))
                        return false;

                    writer = true;
                    writerThreadId = Environment.CurrentManagedThreadId;
                    writeDepth = 1;
                    return true;
                }
                finally
                {
                    writerWaiting = false;
                    Monitor.PulseAll(sync);
                }
            }
        }

        /// <summary>Upgrade the current thread's upgradeable lease to a write lease.</summary>
        internal bool UpgradeToWrite(int timeoutMilliseconds)
        {
            lock (sync)
            {
                if (disposed) return false;

                if (writer && writerThreadId == Environment.CurrentManagedThreadId)
                {
                    writeDepth++;
                    return true;
                }

                // Announce the pending upgrade so new readers queue behind it. Without this a
                // stream of short readers can starve a long-running commit until it times out.
                writerWaiting = true;
                try
                {
                    if (!WaitWhile(() => writer || readers > 0, timeoutMilliseconds)) return false;

                    writer = true;
                    writerThreadId = Environment.CurrentManagedThreadId;
                    writeDepth = 1;
                    return true;
                }
                finally
                {
                    writerWaiting = false;
                    Monitor.PulseAll(sync);
                }
            }
        }

        internal void ExitWrite()
        {
            lock (sync)
            {
                if (!writer) return;
                if (--writeDepth > 0) return;

                writer = false;
                writerThreadId = -1;
                writeDepth = 0;
                Monitor.PulseAll(sync);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                Monitor.PulseAll(sync);
            }
        }

        private bool WaitWhile(Func<bool> condition, int timeoutMilliseconds)
        {
            if (!condition()) return true;
            if (disposed) return false;

            if (timeoutMilliseconds < 0)
            {
                while (condition())
                {
                    if (disposed) return false;
                    Monitor.Wait(sync);
                }
                return true;
            }

            var sw = Stopwatch.StartNew();
            while (condition())
            {
                if (disposed) return false;

                int remaining = timeoutMilliseconds - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0) return false;

                Monitor.Wait(sync, remaining);
            }
            return true;
        }
    }
}