using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.LogStructure;
using LumDbEngine.Element.Structure;
using LumDbEngine.IO;
using LumDbEngine.Utils.SemaphoreUtils;
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
        /// Packed on-disk format version (see <see cref="DbHeader.VERSION"/>).
        /// </summary>
        public uint Version => DbHeader.VERSION;

        /// <summary>
        /// Dotted file-format version, e.g. "1.3.9".
        /// </summary>
        public string VersionString => DbHeader.FormatVersion(DbHeader.VERSION);

        /// <summary>
        /// Dotted file-format version of this assembly (same as <see cref="VersionString"/>).
        /// </summary>
        public static string CurrentVersionString => DbHeader.FormatVersion(DbHeader.VERSION);
        private string path = "";
        private IOFactory? iof = null;
        //private readonly ThreadLocal<int> callCount = new ThreadLocal<int>(() => 0);

        /// <summary>
        /// Global lock for the db engine, which is used to make sure only one thread can write the db at a time.
        /// </summary>
        public ReaderWriterLockSlim ReadWriteLock { get; }= new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Database file path.
        /// </summary>
        public string Path { get => path; }

        public TransactionPolicy TransactionPolicy = TransactionPolicy.ReadCommitted;

        private MemoryDbBuffer? memory;

        /// <summary>
        /// Create a memory based db engine.
        /// The same instance shares one committed in-memory image across transactions.
        /// </summary>
        public DbEngine()
        {
            memory = new MemoryDbBuffer();
            InitializeMemory();
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
                    try
                    {
                        dblog.DumpToDbEngine(iof.FileStream);
                    }
                    finally
                    {
                        dblog.Dispose();
                    }
                    break;
                case DbLogState.Done:
                default:
                    DbLog.PrepareForWrite(this, iof.FileStream);
                    break;
            }

        }

        private const int MaxSemaphoreCount = 32;
        //ivate readonly Semaphore resetEvent = new Semaphore(0,1000); 
        private readonly SemaphoreSlim resetEvent = new SemaphoreSlim(MaxSemaphoreCount, MaxSemaphoreCount); 

        private void InitializeNew(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw LumException.Raise("Wrong path");
                }
               // var ck = new STChecker(autoResetEvent, callCount, -1);
                using var ts = new LumTransaction(null, DbCache.DEFAULT_CACHE_PAGES, true, this);
                ts.SaveAs(path);
                ts.Discard();
            }
            catch (LumException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex.Message == LumExceptionMessage.IllegaTransaction || ex.Message == LumExceptionMessage.TransactionTimeout)
                {
                    throw;
                }
                else
                {
                    throw LumException.Raise("Transaction start failed, since DbEngine might be disposed.");
                }
            }

        }

        private void InitializeMemory()
        {
            try
            {
                using var ts = new LumTransaction(null, DbCache.DEFAULT_CACHE_PAGES, true, this);
                ts.SaveToMemory(memory!);
                ts.Discard();
            }
            catch (LumException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex.Message == LumExceptionMessage.IllegaTransaction || ex.Message == LumExceptionMessage.TransactionTimeout)
                {
                    throw;
                }
                else
                {
                    throw LumException.Raise("Transaction start failed, since DbEngine might be disposed.");
                }
            }

            iof = new IOFactory(memory!);
        }

        /// <summary>
        /// Copy the committed database image to a physical file. Uncommitted changes in an open write
        /// transaction are not included. The file can be opened with <see cref="DbEngine(string, bool)"/>.
        /// </summary>
        public void SaveTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw LumException.Raise("Wrong path");

            using var lk = LockTransaction.TryStartRead(ReadWriteLock, TimeoutMilliseconds);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (memory != null)
            {
                File.WriteAllBytes(path, memory.ToArray());
                return;
            }

            if (iof?.FileStream == null)
                throw LumException.Raise("Wrong path");

            lock (iof.FileStream)
            {
                iof.BinaryWriter?.Flush();
                iof.FileStream.Flush();
                var pos = iof.FileStream.Position;
                iof.FileStream.Seek(0, SeekOrigin.Begin);
                using (var dest = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    iof.FileStream.CopyTo(dest);
                iof.FileStream.Seek(pos, SeekOrigin.Begin);
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
                 throw;
             
            }
        }

        /// <summary>
        /// Start a new readonly transaction to execute commands. A started transaction must be disposed when action is done, and should better use with "using" in case of not diosposing. 
        /// </summary>
        /// <param name="initialCachePages"></param>
        /// <param name="dynamicCache"></param>
        /// <returns></returns>
        public ITransactionReadonly StartTransactionReadonly(int initialCachePages = DbCache.DEFAULT_CACHE_PAGES, bool dynamicCache = true)
        {

            try
            {
                return new TransactionReadonly(iof, initialCachePages, dynamicCache, this);
            }
            catch (Exception ex)
            {       
                 throw;
             
            }
        }

        private ConcurrentDictionary<Guid, ITransaction> transactionsPool = new();

        internal bool RegisterTransaction(Guid guid, ITransaction ts)
        {
            try
            {
#if DEBUG
                LumException.ThrowIfTrue(disposed, "dnengine");
#endif
                if (resetEvent.Wait(TimeoutMilliseconds))
                {
                    if (transactionsPool.TryAdd(guid, ts))
                    {
                        return true;
                    }
                    else
                    {
                        resetEvent.Release();
                        throw LumException.Raise(LumExceptionMessage.InternalError);
                    }

                }
                else
                {
                    return false;
                }
            }
            catch
            {
                throw LumException.Raise(LumExceptionMessage.TransactionTimeout);
            }
        }

        internal bool UnregisterTransaction(Guid guid)
        {

                if (transactionsPool.Remove(guid, out _))
                {
                    resetEvent.Release();
                    return true;
                }
                else
                {
                    return false;
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
                    if (resetEvent.WaitAll(MaxSemaphoreCount,TimeoutMilliseconds))
                    {
                        disposed = true;
#if DEBUG
                        LumException.ThrowIfTrue(transactionsPool.Count > 0, "readwriteLock未释放");
#endif

                        ReadWriteLock?.Dispose();

                        iof?.Dispose();
                        iof = null;
                        resetEvent.Dispose();
                        if (DesrotyOnDispose)
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                        }
                    }
                    else
                    {
                        LumException.Throw($"{LumExceptionMessage.DbEngDisposedTimeOut} Living transactions: " +
                            $"{string.Join(';', transactionsPool.Values.Select(o => o.Id.ToString()).ToArray())}");
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