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

        private LockTransaction(ReaderWriterLockSlim readerWriterLockSlim, State isWrite)
        {
            this.readerWriterLockSlim = readerWriterLockSlim;
            this.isWrite = isWrite;

            try
            {
                switch (isWrite)
                {
                    case State.Read:
                        readerWriterLockSlim.EnterReadLock();
                        break;
                    case State.Write:
                        readerWriterLockSlim.EnterWriteLock();
                        break;
                    case State.UpgradeableRead:
                        readerWriterLockSlim.EnterUpgradeableReadLock();
                        break;
                    default:
                        throw new InvalidOperationException("unknow lock state");
                }
            }
            catch (LockRecursionException ex)
            {
                throw new ArgumentException(ex.Message);
            }
        }

        internal static LockTransaction StartRead(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, State.Write);
        }

        internal static LockTransaction StartWrite(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, State.Read);
        }

        internal static LockTransaction StartUpgradeableRead(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, State.UpgradeableRead);
        }

        public void WriteAction(Action act)
        {
            lock (this)
            {
                if (isWrite == State.UpgradeableRead)
                {
                    readerWriterLockSlim.EnterWriteLock();
                    act();
                    readerWriterLockSlim.ExitWriteLock();
                }
                else if (isWrite== State.WriteAfterUpgradeableRead || isWrite == State.Write)
                {
                    act();
                }
            }
        }

        //public void UpgradeToWrite()
        //{
        //    lock (this)
        //    {
        //        if (isWrite == State.UpgradeableRead)
        //        {
        //            readerWriterLockSlim.EnterWriteLock();
        //            isWrite++;
        //        }
        //    }
        //}
        //public void DegradeToRead()
        //{
        //    lock (this)
        //    {
        //        if (isWrite == State.WriteAfterUpgradeableRead)
        //        {
        //            readerWriterLockSlim.ExitWriteLock();
        //            isWrite--;
        //        }
        //    }
        //}

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