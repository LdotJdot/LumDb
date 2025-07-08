using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.LogStructure;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.IO;
using System.Collections.Concurrent;

#nullable disable

namespace LumDbEngine.Element.Engine.Cache
{
    internal partial class DbCache
    {
        private DbHeader header = new DbHeader();

        internal const int MIN_CACHE_PAGES = 128;
        internal const int DEFAULT_CACHE_PAGES = 1024;
        internal long max_cache_pages = DEFAULT_CACHE_PAGES;
        private bool isDynamicCachePages = false;
        internal ConcurrentDictionary<uint, BasePage> pages = new();
        internal IOFactory iof { get; set; } = null;

        internal DbCache(IOFactory iof, long cachePages, bool dynamicCache)
        {
            // todo calculate default cache page count
            if (cachePages < MIN_CACHE_PAGES)
            {
                max_cache_pages = MIN_CACHE_PAGES;
            }
            else
            {
                max_cache_pages = cachePages;
            }

            if (iof == null)
            {
                const uint iniPageId = 0;
                header = new DbHeader();
                header.RootTableRepoPage = iniPageId;
                header.LastPage = iniPageId;
                header.AvailableTableRepoPage = iniPageId;
                header.MarkDirty();
                Expand(header.LastPage);
                var scheme = new RepoPage().Initialize(iniPageId);
                scheme.MarkDirty();
                CachePage(scheme);
            }
            else
            {
                this.isDynamicCachePages = dynamicCache;
                this.iof = iof;
                using var reader = iof.RentReader();
                header.Read(reader);
            }
        }


        /// <summary>
        /// save changes to file
        /// </summary>
        internal void SaveCurrentPageCache(DbEngine dbEngine)
        {

            if (iof?.IsValid() == true && disposed == false)
            {
                var dblog = DbLog.Create(dbEngine);

                // write dblog.
                {
                    dblog.WriteState(DbLogState.Writing);
                    dblog.Write(header);
                    foreach (var page in pages.Values)
                    {
                        if (page?.IsDirty == true)
                        {
                            dblog.Write(page);
                            page.IsDirty = false;
                        }
                    }
                    dblog.WriteState(DbLogState.Done);
                }

                dblog.DumpToDbEngine(iof.FileStream);

                dblog.Dispose();    // make sure the dblog file will remain when the process was abort.

                GarbageCollection();
                //pages.Clear();
            }

        }

        /// <summary>
        /// save changes to a new file
        /// </summary>
        /// <param name="path"></param>
        internal void SaveCurrentPageCache(string path)
        {
            if (disposed == false)
            {
                LumException.ThrowIfTrue(File.Exists(path), "File already existed");

                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                using var fs = File.Create(path);
                using BinaryWriter bw = new BinaryWriter(fs);
                {
                    header.Write(bw);

                    foreach (var page in pages.Values)
                    {
                        if (page?.IsDirty == true)
                        {
                            page.Write(bw);
                            page.IsDirty = false;
                        }
                    }

                    bw.Flush();

                    GarbageCollection();
                }
            }

        }

        private bool disposed = false;

        public void Dispose(DbEngine dbEngine)
        {
            if (!disposed)
            {
                SaveCurrentPageCache(dbEngine);
                PagesClear();
                pages = null;
                disposed = true;                
            }
        }
        public void Dispose()
        {
            if (!disposed)
            {
                PagesClear();
                pages = null;
                disposed = true;                
            }
        }

        public void Reset()
        {
            if (!disposed)
            {
                PagesClear();
            }
        }

        public void PagesClear()
        {
            lock (pages)
            {
                pages.Clear();
            }
        }
    }
}