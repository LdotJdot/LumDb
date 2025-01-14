using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.IO;

namespace LumDbEngine.Element.Engine
{
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
        /// Database file path.
        /// </summary>
        public string Path { get => path; }

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
        }

        private readonly AutoResetEvent autoResetEvent = new AutoResetEvent(true); // make sure serializable

        internal void InitializeNew(string path)
        {
            var ck = new STChecker(autoResetEvent, callCount);
            using var ts = new LumTransaction(null, ck, DbCache.DEFAULT_CACHE_PAGES, true);
            ts.SaveAs(path);
            ts.Discard();
        }


        /// <summary>
        /// Start a new transaction to execute commands. A started transaction must be disposed when action is done, and should better used with "using" in case of lacking diosposing. 
        /// </summary>
        /// <param name="initialCachePages">Initial cache page size. The minimal page shoud be large than 128</param>
        /// <param name="dynamicCache">System manage the cache page automatically</param>
        /// <returns></returns>
        public ITransaction StartTransaction(int initialCachePages = DbCache.DEFAULT_CACHE_PAGES, bool dynamicCache = true)
        {
            var ck = new STChecker( autoResetEvent,callCount);
            return new LumTransaction(iof, ck, initialCachePages, dynamicCache);
        }

        private bool disposed;

        /// <summary>
        /// Dispose the current engine and free the db file usage (if have).
        /// </summary>
        public void Dispose()
        {
            if (disposed == false)
            {
                disposed = true;
                autoResetEvent?.Dispose();
                iof?.Dispose();
                iof = null;
            }
        }

        /// <summary>
        /// Physically delete the current db file on disk
        /// </summary>
        public void Destory()
        {
            Dispose();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}