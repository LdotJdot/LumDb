using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.IO;

namespace LumDbEngine.Element.Engine
{
    public class DbEngine : IDisposable
    {
        public uint Version => DbHeader.VERSION;
        private string path = "";
        private IOFactory? iof = null;
        public string Path { get => path; }

        /// <summary>
        /// Run in memory only
        /// </summary>
        public DbEngine()
        {
        }

        /// <summary>
        /// Run based on file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="createIfNotExists"></param>
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

        public void InitializeNew(string path)
        {
            autoResetEvent.WaitOne();
            using var ts = new LumTransaction(null, autoResetEvent, DbCache.DEFAULT_CACHE_PAGES, true);
            ts.SaveAs(path);
            ts.Discard();
        }

        /// <summary>
        /// Start a new transaction
        /// </summary>
        /// <param name="initialCachePages">Minimal page shoud be large than 128</param>
        /// <param name="dynamicCache"></param>
        /// <returns></returns>
        public ITransaction StartTransaction(int initialCachePages = DbCache.DEFAULT_CACHE_PAGES, bool dynamicCache = true)
        {
            LumException.ThrowIfTrue(disposed, "Can not access a disposed transaction.");
            autoResetEvent.WaitOne();
            return new LumTransaction(iof, autoResetEvent, initialCachePages, dynamicCache);
        }

        private bool disposed;

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
        /// Delete the current db file on disk
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