using LumDbEngine.Element.Exceptions;

namespace LumDbEngine.Element.Engine.Lock
{
    /// <summary>
    /// A lease on an <see cref="EngineGate"/>. Unlike ReaderWriterLockSlim, the lease is released
    /// by counters inside the gate, so Dispose() is safe from any thread (async callers included).
    /// </summary>
    internal class LockTransaction : IDisposable
    {
        private EngineGate? gate;
        private State isWrite;
        private readonly int timeoutMilliseconds;
        private int disposed;

        enum State
        {
            Read = 0,
            Write = 1,
            UpgradeableRead = 2,
        }

        private LockTransaction(EngineGate gate, State initialLockState, int timeoutMillionSeconds)
        {
            this.gate = gate;
            this.isWrite = initialLockState;
            this.timeoutMilliseconds = timeoutMillionSeconds;

            try
            {
                switch (initialLockState)
                {
                    case State.Read:
                        if (!gate.EnterRead(timeoutMillionSeconds))
                            throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
                        break;

                    case State.Write:
                        if (!gate.EnterWrite(timeoutMillionSeconds))
                            throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
                        break;

                    case State.UpgradeableRead:
                        if (!gate.EnterUpgradeable(timeoutMillionSeconds))
                            throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
                        break;

                    default:
                        throw LumException.Raise(LumExceptionMessage.IllegaTransaction);
                }
            }
            catch
            {
                this.gate = null;
                throw;
            }
        }

        internal static LockTransaction TryStartRead(EngineGate gate, int timeoutMilliseconds)
            => new LockTransaction(gate, State.Read, timeoutMilliseconds);

        internal static LockTransaction TryStartWrite(EngineGate gate, int timeoutMilliseconds)
            => new LockTransaction(gate, State.Write, timeoutMilliseconds);

        internal static LockTransaction TryStartUpgradeableRead(EngineGate gate, int timeoutMilliseconds)
            => new LockTransaction(gate, State.UpgradeableRead, timeoutMilliseconds);

        /// <summary>
        /// Run a mutation while holding the engine write lease. The lease is always returned, even
        /// when <paramref name="act"/> throws (a failed commit used to leak the write lock and
        /// brick the engine).
        /// </summary>
        public void WriteAction(Action act)
        {
            switch (isWrite)
            {
                case State.Read:
                    // Legacy behaviour: a read lease silently ignores write actions.
                    return;

                case State.Write:
                    act();
                    return;

                case State.UpgradeableRead:
                    var g = gate ?? throw new InvalidOperationException("lock already disposed");
                    // Bounded like the old TryEnterWriteLock: an upgrade that cannot drain the
                    // readers must fail loudly instead of blocking the caller forever.
                    if (!g.UpgradeToWrite(timeoutMilliseconds))
                        throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
                    try
                    {
                        act();
                    }
                    finally
                    {
                        g.ExitWrite();
                    }
                    return;

                default:
                    throw new InvalidOperationException("unknow lock state");
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;

            var g = gate;
            gate = null;
            if (g == null) return;

            switch (isWrite)
            {
                case State.Read:
                    g.ExitRead();
                    break;
                case State.Write:
                    g.ExitWrite();
                    break;
                case State.UpgradeableRead:
                    g.ExitUpgradeable();
                    break;
                default:
                    throw new InvalidOperationException("unknow lock state");
            }
        }
    }
}