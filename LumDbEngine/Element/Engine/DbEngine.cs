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
    public partial class DbEngine : IDisposable
    {
        /// <summary>
        /// Packed on-disk page format (see <see cref="DbHeader.VERSION"/>).
        /// This is not the assembly or NuGet package version.
        /// </summary>
        public uint Version => DbHeader.VERSION;

        /// <summary>
        /// Dotted page-format version, e.g. "1.3.9".
        /// </summary>
        public string VersionString => DbHeader.FormatVersion(DbHeader.VERSION);

        /// <summary>
        /// Dotted page-format version of this assembly (same as <see cref="VersionString"/>).
        /// </summary>
        public static string CurrentVersionString => DbHeader.FormatVersion(DbHeader.VERSION);
        private string path = "";
        private IOFactory? iof = null;

        private long commitGeneration;

        /// <summary>
        /// Image a transaction must bind. Read it only after this engine's lock is held,
        /// so a vacuum swap cannot hand out a file that is about to be replaced.
        /// </summary>
        internal IOFactory? CurrentImage => iof;

        internal void NoteCommit() => Interlocked.Increment(ref commitGeneration);
        //private readonly ThreadLocal<int> callCount = new ThreadLocal<int>(() => 0);

        /// <summary>
        /// Engine-wide reader/writer gate. Deliberately thread-agnostic: a transaction lease must be
        /// releasable from any thread (async callers dispose transactions on another thread).
        /// Same-thread readers may nest. A second write transaction on the same thread is rejected
        /// in <see cref="LumTransaction"/> so two caches cannot overwrite each other.
        /// </summary>
        internal EngineGate Gate { get; } = new EngineGate();

        /// <summary>
        /// Database file path.
        /// </summary>
        public string Path { get => path; }

        private TransactionPolicy transactionPolicy = TransactionPolicy.ReadCommitted;

        /// <summary>
        /// Transaction isolation policy. Only <see cref="TransactionPolicy.ReadCommitted"/> is
        /// implemented; <see cref="TransactionPolicy.Serializable"/> is rejected when a transaction
        /// is started instead of being silently ignored, and unknown values are rejected here.
        /// </summary>
        public TransactionPolicy TransactionPolicy
        {
            get => transactionPolicy;
            set
            {
                if (value != TransactionPolicy.ReadCommitted && value != TransactionPolicy.Serializable)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transaction policy.");
                transactionPolicy = value;
            }
        }

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

            var opened = new IOFactory(path);
            this.iof = opened;
            try
            {
                // Reject foreign or corrupt files before any recovery writes to them.
                DbHeader.ValidateImage(opened);

                DbLogState state;
                using (var reader = opened.RentReader())
                    state = DbLogUtils.CheckDbState(reader);

                switch (state)
                {
                    case DbLogState.Writing:
                        RecoverOrRepairWritingState();
                        break;
                    case DbLogState.Done:
                    default:
                        DbLog.PrepareForWrite(this, opened.FileStream);
                        break;
                }
            }
            catch
            {
                opened.Dispose();
                this.iof = null;
                throw;
            }

        }

        /// <summary>
        /// A Writing header with no log has nothing to replay. The pages are the last image
        /// that made it to the file, so repair the state byte instead of refusing to open.
        /// A log that exists is still replayed, or rejected when it is incomplete.
        /// </summary>
        private void RecoverOrRepairWritingState()
        {
            var logState = DbLogUtils.CheckLogState(path + ".log");
            if (logState == DbLogState.NotExisted)
            {
                DbLogUtils.MarkDbState(iof!.FileStream, DbLogState.Done);
                return;
            }

            var dblog = DbLog.OpenLogToRecoveryDbEngine(this);
            try
            {
                dblog.DumpToDbEngine(iof!.FileStream);
            }
            finally
            {
                dblog.Dispose();
            }
        }

        /// <summary>
        /// Maximum number of transactions open at the same time on one engine. Kept as a hard cap
        /// by design; exceeding it fails fast instead of pretending the engine was disposed.
        /// </summary>
        public const int MaxConcurrentTransactions = 32;

        /// <summary>
        /// How long a new transaction waits for a free slot in the transaction pool.
        /// Default 0 = fail immediately once <see cref="MaxConcurrentTransactions"/> is reached.
        /// </summary>
        public int TransactionPoolWaitMilliseconds { get; set; } = 0;

        private readonly SemaphoreSlim resetEvent = new SemaphoreSlim(MaxConcurrentTransactions, MaxConcurrentTransactions);

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

            // Bug fix BUG-07: take a Write lock so we cannot grab a torn snapshot while another
            // transaction is mutating pages. A Read lock allowed concurrent writers to flush
            // half-committed pages between the CopyTo() snapshot points.
            using var lk = LockTransaction.TryStartWrite(Gate, TimeoutMilliseconds);

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
            if (TransactionPolicy == TransactionPolicy.Serializable)
                throw new NotSupportedException(
                    "Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.");

            return new LumTransaction(null, initialCachePages, dynamicCache, this, bindLiveImage: true);
        }

        /// <summary>
        /// Start a new readonly transaction to execute commands. A started transaction must be disposed when action is done, and should better use with "using" in case of not diosposing. 
        /// </summary>
        /// <param name="initialCachePages"></param>
        /// <param name="dynamicCache"></param>
        /// <returns></returns>
        public ITransactionReadonly StartTransactionReadonly(int initialCachePages = DbCache.DEFAULT_CACHE_PAGES, bool dynamicCache = true)
        {
            if (TransactionPolicy == TransactionPolicy.Serializable)
                throw new NotSupportedException(
                    "Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.");

            return new TransactionReadonly(null, initialCachePages, dynamicCache, this, bindLiveImage: true);
        }

        private ConcurrentDictionary<Guid, ITransaction> transactionsPool = new();

        /// <summary>
        /// Two live transactions on one thread would each hold their own page cache and overwrite
        /// each other's commit, so a write transaction is rejected while any transaction of this
        /// thread is still open. Read-only transactions may nest (they never commit).
        /// </summary>
        internal void EnsureNoTransactionOnCurrentThread()
        {
            int current = Environment.CurrentManagedThreadId;
            foreach (var pair in transactionsPool)
            {
                if (pair.Value is LumTransaction tx && tx.OwnerThreadId == current)
                    throw LumException.Raise(LumExceptionMessage.IllegaTransaction);
            }
        }

        /// <summary>
        /// Reserve a transaction slot. Throws instead of returning false so callers can tell
        /// "engine disposed" apart from "pool full"; the previous catch rewrote every failure into
        /// TransactionTimeout, which also hid InternalError.
        /// </summary>
        internal void RegisterTransaction(Guid guid, ITransaction ts)
        {
            if (Volatile.Read(ref disposed) != 0)
                throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);

            if (!resetEvent.Wait(TransactionPoolWaitMilliseconds))
                throw LumException.Raise(
                    $"{LumExceptionMessage.TooManyOpenTransactions} Limit: {MaxConcurrentTransactions}, currently open: {transactionsPool.Count}.");

            if (!transactionsPool.TryAdd(guid, ts))
            {
                resetEvent.Release();
                throw LumException.Raise(LumExceptionMessage.InternalError);
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

        // Bug fix BUG-14: `disposed` is read across threads without synchronization. Promote the field
        // to an int read/written via Volatile so other threads observe the transition reliably.
        // Semantics are unchanged: 0 = alive, non-zero = disposed.
        internal int disposed;

        /// <summary>
        /// The milliseconds timeout when the dbEngine dispose waiting for the transaction end. Default value is 3000ms, 0 which means not waiting.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 3000;
        /// <summary>
        /// Dispose the current engine and free the db file usage (if have).
        /// </summary>

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0) return;
            // Mark disposed up-front so concurrent transactions observe the transition immediately,
            // even before we finish waiting for them. They will see disposed != 0 and abort early.
            // A timed-out wait rolls the flag back: cleanup did not run, and a later Dispose must retry.
            if (resetEvent.WaitAll(MaxConcurrentTransactions, TimeoutMilliseconds))
            {
#if DEBUG
                LumException.ThrowIfTrue(transactionsPool.Count > 0, "readwriteLock未释放");
#endif

                Gate?.Dispose();

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
                Volatile.Write(ref disposed, 0);
                LumException.Throw($"{LumExceptionMessage.DbEngDisposedTimeOut} Living transactions: " +
                    $"{string.Join(';', transactionsPool.Values.Select(o => o.Id.ToString()).ToArray())}");
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