using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.LogStructure;
using LumDbEngine.Element.Structure;
using LumDbEngine.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace LumDbEngine.Element.Engine
{
    public enum TransactionPolicy
    {
        ReadCommitted = 1,
        Serializable=2
    }
    /// <summary>
    /// The main db engine
    /// </summary>
    public class DbEngine : IDisposable
    {
        /// <summary>
        /// Version of LumDb
        /// </summary>
        public uint Version => DbHeader.VERSION;
        private string path = "";
        private IOFactory? iof = null;
        private readonly ThreadLocal<int> callCount = new ThreadLocal<int>(() => 0);

        /// <summary>
        /// Global lock for the db engine, which is used to make sure only one thread can write the db at a time.
        /// </summary>
        public ReaderWriterLockSlim ReadWriteLock { get; }= new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Database file path.
        /// </summary>
        public string Path { get => path; }

        public TransactionPolicy TransactionPolicy = TransactionPolicy.ReadCommitted;

        /// <summary>
        /// Create a memory based db engine.
        /// </summary>
        public DbEngine()
        {
        }

        /// <summary>
        /// Create a file exclusive db engine.
        /// </summary>
        /// <param name="path">db file path</param>
        /// <param name="createIfNotExists">create a new db file if the path does not exist</param>
        public DbEngine(string path, bool createIfNotExists = true)
        {
            this.path = path;
              InitializeEngine(this.path, createIfNotExists);
        }

        private void InitializeEngine(string path, bool createIfNotExists)
        {
            if (!File.Exists(path))
            {
                if (createIfNotExists)
                {
                    InitializeNew(path);
                }
                else
                {
                    throw new FileNotFoundException($"File not found:{path}");
                }
            }

            this.iof = new IOFactory(path);
            var state = DbLogUtils.CheckDbState(iof.RentReader());

            switch (state)
            {
                case DbLogState.Writing:
                    var dblog = DbLog.OpenLogToRecoveryDbEngine(this);
                    dblog.DumpToDbEngine(iof.FileStream);
                    dblog.Dispose(); // make sure the log can be normally disposed when no exception was throw.

                    break;
                case DbLogState.Done:
                default:
                    break;
            }

        }

        private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(true); 

        private void InitializeNew(string path)
        {

            try
            {
               // var ck = new STChecker(autoResetEvent, callCount, -1);
                using var ts = new LumTransaction(null, DbCache.DEFAULT_CACHE_PAGES, true, this);
                ts.SaveAs(path);
                ts.Discard();
            }
            catch (Exception ex)
            {
                if (ex.Message == LumExceptionMessage.SingleThreadMultiTransaction || ex.Message == LumExceptionMessage.TransactionTimeout)
                {
                    throw;
                }
                else
                {
                    throw LumException.Raise("Transaction start failed, since DbEngine might be disposed.");
                }
            }

        }


        /// <summary>
        /// Start a new transaction to execute commands. A started transaction must be disposed when action is done, and should better use with "using" in case of not diosposing. 
        /// </summary>
        /// <param name="initialCachePages">Initial cache page size. The minimal page shoud be large than 128</param>
        /// <param name="dynamicCache">System manage the cache page automatically</param>
        /// <returns></returns>
        public ITransaction StartTransaction(int initialCachePages = DbCache.DEFAULT_CACHE_PAGES, bool dynamicCache = true)
        {

            try
            {
                return new LumTransaction(iof, initialCachePages, dynamicCache, this);
            }
            catch (Exception ex)
            {
                if (ex.Message == LumExceptionMessage.SingleThreadMultiTransaction || ex.Message == LumExceptionMessage.TransactionTimeout)
                {
                    throw;
                }
                else
                {
                    throw LumException.Raise("Transaction start failed, and the dbEngine might have been disposed.");
                }
            }

        }

        private Dictionary<Guid, ITransaction> transactionsPool = new();
        
        internal void RegisterTransaction(Guid guid, ITransaction ts)
        {
            lock (transactionsPool)
            {
                resetEvent.Reset();
                transactionsPool.TryAdd(guid, ts);
            }
        }

        internal void UnregisterTransaction(Guid guid)
        {
            lock (transactionsPool)
            {
                transactionsPool.Remove(guid, out _);
                if(transactionsPool.Count == 0)
                {
                    resetEvent.Set();
                }
            }

        }

        /// <summary>
        /// Get transaction id.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="ts"></param>
        /// <returns></returns>
        internal bool GetById(Guid id, out ITransaction ts)
        {
            return transactionsPool.TryGetValue(id, out ts);
        }

        internal bool disposed;

        /// <summary>
        /// The milliseconds timeout when the dbEngine dispose waiting for the transaction end. Default value is 3000ms, 0 which means not waiting.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 3000;
        /// <summary>
        /// Dispose the current engine and free the db file usage (if have).
        /// </summary>
        public void Dispose()
        {
            if (disposed == false)
            {

                    if (TimeoutMilliseconds > 0)
                    {

                        resetEvent.Wait(TimeoutMilliseconds);
                        if (transactionsPool.Count > 0)
                        {
                            LumException.Throw($"{LumExceptionMessage.DbEngDisposedTimeOut} Living transactions: " +
                                $"{string.Join(';', transactionsPool.Values.Select(o => o.Id.ToString()).ToArray())}");
                        }

                    }

                if (resetEvent.Wait(TimeoutMilliseconds))
                {
                    lock (transactionsPool)
                    {
                        ReadWriteLock?.Dispose();

                        iof?.Dispose();
                        iof = null;
                        resetEvent?.Dispose();
                        disposed = true;
                        if (DesrotyOnDispose)
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set desrotyOnDispose to be true and physically delete the current db file on disk when disposed.
        /// </summary>
        public void SetDestoryOnDisposed()
        {
            DesrotyOnDispose = true;
        }

        /// <summary>
        /// Physically delete the current db file on disk when disposed.
        /// </summary>
        private bool DesrotyOnDispose { get; set; } = false;

    }
}