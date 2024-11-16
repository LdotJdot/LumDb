using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Engine.Cache
{
    internal partial class DbCache
    {
        internal void Expand(uint pageId)
        {
            if (pageId == uint.MaxValue)
            {
                throw new InvalidOperationException(nameof(pageId));
            }

            pages.TryAdd(pageId, null);

            if (header.LastPage < pageId || header.LastPage == uint.MaxValue)
            {
                SetLastPageID(pageId);
            }
        }

        private void LoadPage(uint pageId)
        {
            if (!pages.ContainsKey(pageId))
            {
                lock (pages)
                {
                    if (!pages.ContainsKey(pageId))
                    {
                        if (iof != null)
                        {
                            // todo need compare the strategy of insert and gc

                            GarbageCollection();

                            if (isDynamicCachePages && pages.Count > max_cache_pages)
                            {
                                max_cache_pages *= 2;
                                Console.WriteLine("cache size: " + max_cache_pages);
                            }

                            using var reader = iof.RentReader();
                            pages.TryAdd(pageId, PageManager.Load(reader, pageId));
                        }
                    }
                }
            }
        }

        private readonly List<uint> garbageCache = new(64);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GarbageCollection()
        {
            if (iof != null && (pages.Count > max_cache_pages))
            {
                garbageCache.Clear();

                foreach (var page in pages.Values)
                {
                    if (page?.IsDirty == false)
                    {
                        garbageCache.Add(page.PageId);
                    }
                }

                if (garbageCache.Count > 0)
                {
                    foreach (var pageId in garbageCache)
                    {
                        pages.Remove(pageId, out _);
                    }
                }
            }
        }

        internal void CachePage(BasePage page)
        {
            if (pages.ContainsKey(page.PageId))
            {
                pages[page.PageId] = page;
            }
            else
            {
                throw LumException.Raise($"Current page was not in pool (pageId):{page.PageId}.");
            }
        }

        internal BasePage? this[uint index]
        {
            get
            {
                if (!pages.ContainsKey(index))
                {
                    LoadPage(index);
                }

                if (pages.TryGetValue(index, out var value))
                {
                    return value;
                }

                throw LumException.Raise("Incorrected page data.");
            }

            set
            {
                if (value != null)
                {
                    CachePage(value);
                }
                else
                {
                    throw LumException.Raise("buffer page is empty.");
                }
            }
        }

        internal void MarkDirtyAndCachePage(BasePage? page)
        {
            if (page?.IsDirty == false)
            {
                page.MarkDirty();
                pages.TryAdd(page.PageId, page);
            }
        }

        internal void MarkDirtyAndCachePage(DbCache db, uint pageId)
        {
            var page = PageManager.GetPage(db, pageId);
            MarkDirtyAndCachePage(page);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsValidPage(BasePage? page)
        {
            return page?.PageId <= header.LastPage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsInitialized()
        {
            return IsValidPage(header.RootTableRepoPage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsValidPage(uint? pageId)
        {
            return pageId <= header.LastPage;
        }
    }
}