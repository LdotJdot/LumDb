namespace LumDbEngine.Element.Engine.Lock
{
    internal readonly ref struct LockTransaction
    {
        private readonly ReaderWriterLockSlim readerWriterLockSlim;
        private readonly bool isWrite = false;

        public LockTransaction(ReaderWriterLockSlim readerWriterLockSlim, bool isWrite)
        {
            this.readerWriterLockSlim = readerWriterLockSlim;
            this.isWrite = isWrite;

            try
            {
                if (isWrite)
                {
                    readerWriterLockSlim.EnterWriteLock();
                }
                else
                {
                    readerWriterLockSlim.EnterReadLock();
                }
            }
            catch (LockRecursionException ex)
            {
                throw new ArgumentException(ex.Message);
            }
        }

        internal static LockTransaction StartRead(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, false);
        }

        internal static LockTransaction StartWrite(ReaderWriterLockSlim writerLockSlim)
        {
            return new LockTransaction(writerLockSlim, true);
        }

        public void Dispose()
        {
            if (isWrite)
            {
                readerWriterLockSlim?.ExitWriteLock();
            }
            else
            {
                readerWriterLockSlim?.ExitReadLock();
            }
        }
    }
}