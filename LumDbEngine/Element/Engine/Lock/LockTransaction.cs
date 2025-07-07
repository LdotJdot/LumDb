using LumDbEngine.Element.Exceptions;

namespace LumDbEngine.Element.Engine.Lock
{  
    internal class LockTransaction:IDisposable
    {
        private readonly ReaderWriterLockSlim readerWriterLockSlim;
        private State isWrite = 0;

        enum State
        {
            Read = 0,
            Write = 1,
            UpgradeableRead = 2,
            WriteAfterUpgradeableRead = 3
        }
        private int timeoutMillionSeconds = -1;
        private LockTransaction(ReaderWriterLockSlim readerWriterLockSlim, State initialLockState, int timeoutMillionSeconds)
        {
            this.readerWriterLockSlim = readerWriterLockSlim;
            this.isWrite = initialLockState;
            this.timeoutMillionSeconds = timeoutMillionSeconds;

            try
            {
                switch (initialLockState)
                {              

                    case State.Read:

                        if (readerWriterLockSlim.TryEnterReadLock(this.timeoutMillionSeconds))
                        {
                            break;
                        }
                        else
                        {
                            goto default;
                        }
                    case State.Write:
                        if (readerWriterLockSlim.TryEnterWriteLock(this.timeoutMillionSeconds))
                        {
                            break;
                        }
                        else
                        {
                            goto default;
                        }
                    case State.UpgradeableRead:
                        if (readerWriterLockSlim.TryEnterUpgradeableReadLock(this.timeoutMillionSeconds))
                        {
                            break;
                        }
                        else
                        {
                            goto default;
                        }
                    default:
                        throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
                }
            }
            catch (LockRecursionException)
            {
                throw LumException.Raise(LumExceptionMessage.IllegaTransaction);
            }
            catch
            {
                throw;
            }

        }

        internal static LockTransaction StartRead(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, State.Read, -1);
        }

        internal static LockTransaction StartWrite(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, State.Write, -1);
        }

        internal static LockTransaction StartUpgradeableRead(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, State.UpgradeableRead, -1);
        }

        internal static LockTransaction TryStartRead(ReaderWriterLockSlim writerLockSlim,int timeoutMilliseconds)
        {
            return new LockTransaction(writerLockSlim, State.Read, timeoutMilliseconds);
        }

        internal static LockTransaction TryStartWrite(ReaderWriterLockSlim writerLockSlim, int timeoutMilliseconds)
        {
            return new LockTransaction(writerLockSlim, State.Write, timeoutMilliseconds);
        }

        internal static LockTransaction TryStartUpgradeableRead(ReaderWriterLockSlim writerLockSlim, int timeoutMilliseconds)
        {
            return new LockTransaction(writerLockSlim, State.UpgradeableRead, timeoutMilliseconds);
        }

        public void WriteAction(Action act)
        {
            lock (this)
            {
                if (isWrite == State.UpgradeableRead)
                {
                    if (readerWriterLockSlim.TryEnterWriteLock(timeoutMillionSeconds))
                    {
                        act();
                        readerWriterLockSlim.ExitWriteLock();
                    }
                    else
                    {
                        throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
                    }
                }
                else if (isWrite== State.WriteAfterUpgradeableRead || isWrite == State.Write)
                {
                    act();
                }
            }
        }


        public void Dispose()
        {
            lock (this)
            {
                switch (isWrite)
                {
                    case State.Read:
                        readerWriterLockSlim.ExitReadLock();
                        break;
                    case State.Write:
                        readerWriterLockSlim.ExitWriteLock();
                        break;
                    case State.UpgradeableRead:
                        readerWriterLockSlim.ExitUpgradeableReadLock();
                        break;
                    case State.WriteAfterUpgradeableRead:
                        readerWriterLockSlim.ExitWriteLock();
                        readerWriterLockSlim.ExitUpgradeableReadLock();
                        break;
                    default:
                        throw new InvalidOperationException("unknow lock state");
                }
            }
        }
    }
}