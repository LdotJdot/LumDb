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
        // Bug fix BUG-02: `pages` must never be null because concurrent callers lock on it (Dispose path).
        // The previous implementation set `pages = null` after Dispose, which then NRE'd any subsequent
        // `lock(pages)` (notably PagesClear / SaveCurrentPageCache via PagesClear path). We now keep the
        // dictionary instance alive and synchronize on a dedicated `_syncRoot` object.
        internal ConcurrentDictionary<uint, BasePage> pages = new();
        private readonly object _syncRoot = new();
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
        /// save changes to file or in-memory image
        /// </summary>
        internal void SaveCurrentPageCache(DbEngine dbEngine)
        {

            if (iof?.IsValid() == true && disposed == false)
            {
                if (iof.IsMemory)
                {
                    if (WriteCommittedImage(iof.BinaryWriter, skipIfNoDirtyPages: true))
                        dbEngine.NoteCommit();
                    return;
                }

                var dirtyPages = pages.Values.Where(page => page?.IsDirty == true).ToList();
                // Nothing to commit: do not create (or touch) the .log at all. A commit of an
                // unmodified working set used to fail when the log path was unavailable, which
                // turned a plain `using` block into an IO error on an otherwise healthy engine.
                if (dirtyPages.Count == 0 && !header.IsDirty)
                    return;

                var dblog = DbLog.Create(dbEngine, iof.FileStream);
                var committed = false;
                try
                {
                    dblog.WriteState(DbLogState.Writing);
                    dblog.Write(header);
                    foreach (var page in dirtyPages)
                    {
                        dblog.Write(page);
                    }
                    dblog.WriteState(DbLogState.Done);

                    dblog.DumpToDbEngine(iof.FileStream);
                    committed = true;
                    header.State = (byte)DbLogState.Done;

                    foreach (var page in dirtyPages)
                    {
                        page.IsDirty = false;
                    }
                }
                finally
                {
                    // Keep the log when the replay failed: it still holds the complete new image.
                    dblog.Close(deleteLog: committed);
                }

                dbEngine.NoteCommit();

                GarbageCollection();
                //pages.Clear();
            }

        }

        /// <summary>
        /// True when a commit would have something to write. Lets Dispose skip the engine write
        /// lease (and the wait for readers) when the transaction never changed a page.
        /// </summary>
        internal bool HasPendingChanges =>
            !disposed && (header.IsDirty || pages.Values.Any(page => page is { IsDirty: true }));

        internal void WriteToMemory(MemoryDbBuffer buffer)
        {
            using var stream = new MemoryCursorStream(buffer, writable: true);
            using var bw = new BinaryWriter(stream);
            WriteCommittedImage(bw, skipIfNoDirtyPages: false);
        }

        private bool WriteCommittedImage(BinaryWriter bw, bool skipIfNoDirtyPages)
        {
            if (disposed)
                return false;

            var dirtyPages = pages?.Values.Where(page => page is { IsDirty: true }).ToList()
                              ?? new List<BasePage>();

            if (skipIfNoDirtyPages)
            {
                // Discard + Dispose must not clobber the committed image with an empty/reset cache.
                if (pages.Count == 0)
                    return false;
                if (dirtyPages.Count == 0 && !header.IsDirty)
                    return false;
            }

            header.State = (byte)DbLogState.Done;
            lock (bw.BaseStream)
            {
                header.Write(bw);
                foreach (var page in dirtyPages)
                    page.Write(bw);
                bw.Flush();
            }

            foreach (var page in dirtyPages)
                page.IsDirty = false;

            GarbageCollection();
            return true;
        }

        /// <summary>
        /// save changes to a new file
        /// </summary>
        /// <param name="path"></param>
        internal void SaveCurrentPageCache(string path)
        {
            if (disposed == false)
            {
                var fileName= Path.GetFileName(path);

                var dir = Path.GetDirectoryName(path);
                if(string.IsNullOrWhiteSpace(dir)) dir= AppDomain.CurrentDomain.BaseDirectory;

                path= Path.Combine(dir, fileName);

                LumException.ThrowIfTrue(File.Exists(path), "File already existed");

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var fs = File.Create(path);
                using BinaryWriter bw = new BinaryWriter(fs);
                {
                    // Export the whole image. After SaveChanges the pages are clean, so a
                    // dirty-only copy is just a header and cannot be opened.
                    header.State = (byte)DbLogState.Done;
                    header.Write(bw);

                    if (header.LastPage != uint.MaxValue)
                    {
                        for (uint pageId = 0; pageId <= header.LastPage; pageId++)
                        {
                            var page = this[pageId];
                            LumException.ThrowIfNull(page, $"missing page {pageId}");
                            page.Write(bw);
                        }
                    }

                    bw.Flush();
                    fs.Flush(true);

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
                disposed = true;
            }
        }
        public void Dispose()
        {
            if (!disposed)
            {
                PagesClear();
                disposed = true;
            }
        }

        public void Reset()
        {
            if (!disposed)
            {
                PagesClear();
                if (iof?.IsValid() == true)
                {
                    using var reader = iof.RentReader();
                    header.Read(reader);
                }
            }
        }

        public void PagesClear()
        {
            lock (_syncRoot)
            {
                pages.Clear();
            }
        }
    }
}