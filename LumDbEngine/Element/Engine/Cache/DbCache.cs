using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.IO;
using System.Collections.Concurrent;

#nullable disable

namespace LumDbEngine.Element.Engine.Cache
{
    internal partial class DbCache : IDisposable
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

        private readonly object saveLock = new();

        /// <summary>
        /// save changes to file
        /// </summary>
        internal void SaveChanges()
        {
            lock (saveLock)
            {
                if (iof?.IsValid() == true && disposed == false)
                {
                    header.Write(iof.BinaryWriter);

                    foreach (var page in pages.Values)
                    {
                        if (page?.IsDirty == true)
                        {
                            page.Write(iof.BinaryWriter);
                            page.IsDirty = false;
                        }
                    }

                    iof.BinaryWriter.Flush();

                    GarbageCollection();
                    //pages.Clear();
                }
            }
        }

        /// <summary>
        /// save changes to a new file
        /// </summary>
        /// <param name="path"></param>
        internal void SaveChanges(string path)
        {
            lock (saveLock)
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
        }

        private bool disposed = false;

        public void Discard()
        {
            pages = new();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                SaveChanges();
                PagesClear();
                pages = null;
                disposed = true;
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