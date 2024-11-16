using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.KeyIndex;
using LumDbEngine.Element.Structure.Page.Repo;
using System.Diagnostics;

namespace LumDbEngine.Element.Manager.Common
{
    internal class PageManager
    {
        public static T RequestAvailablePage<T>(DbCache db) where T : BasePage, new()
        {
            var pageId = RequestFreePageId(db);
            //using MemTest.MemChecker memChecker = new MemTest.MemChecker("RequestAvailablePage");

            T page = (T)new T();
            page.Initialize(pageId);
            //memChecker.Dispose();

            LumException.ThrowIfNull(page, "unkown error");
            db.CachePage(page);
            page.MarkDirty();
            return page;
        }

        internal static RepoPage? GetRootTableRepoPage(DbCache db)
        {
            if (!db.IsInitialized())
            {
                db.Expand(DbHeader.ROOT_INDEX);
                var page = new RepoPage().Initialize(DbHeader.ROOT_INDEX);
                db.SetRootPageID(page.PageId);
                db.SetAvailableTableRepoId(page.PageId);
                db.CachePage(page);
                page.MarkDirty();
            }

            return db[db.RootPageId] as RepoPage;
        }

        internal static uint RequestFreePageId(DbCache db)
        {
            if (db.IsValidPage(db.FreePage))
            {
                var freePage = db[db.FreePage];

                db.SetFreePageID(freePage?.NextPageId ?? uint.MaxValue);
                freePage?.Reset();

                return freePage?.PageId ?? uint.MaxValue;
            }
            else
            {
                db.SetLastPageID(db.LastPage + 1);

                db.Expand(db.LastPage);
                return db.LastPage;
            }
        }

        internal static T GetPage<T>(DbCache db, uint pageId) where T : BasePage
        {
            var p = db[pageId];
            T page = (T)p;
            Debug.Assert(page != null);
            return page!;
        }

        internal static BasePage GetPage(DbCache db, uint pageId)
        {
            var page = db[pageId];
            Debug.Assert(page != null);
            return page;
        }

        internal static void LinkPage(BasePage prevPage, BasePage nextPage)
        {
            if (prevPage != null && nextPage != null)
            {
                prevPage.MarkDirty();
                prevPage.NextPageId = nextPage.PageId;

                nextPage.MarkDirty();
                nextPage.PrevPageId = prevPage.PageId;
            }
        }

        internal static void RecyclePage(DbCache db, BasePage page)
        {
            page.MarkDirty();

            var isPrevValid = db.IsValidPage(page.PrevPageId);
            var isNextValid = db.IsValidPage(page.NextPageId);

            if (isPrevValid && isNextValid)
            {
                Debug.Assert(page.NextPageId != page.PrevPageId);
                var prevPage = GetPage<BasePage>(db, page.PrevPageId);
                var nextPage = GetPage<BasePage>(db, page.NextPageId);

                LinkPage(prevPage, nextPage);
            }
            else if (isPrevValid)
            {
                var prevPage = GetPage<BasePage>(db, page.PrevPageId);
                prevPage.NextPageId = uint.MaxValue;
                prevPage.MarkDirty();
            }
            else if (isNextValid)
            {
                var nextPage = GetPage<BasePage>(db, page.NextPageId);
                nextPage.PrevPageId = uint.MaxValue;
                nextPage.MarkDirty();
            }

            var freePageId = db.FreePage;

            Debug.Assert(freePageId != page.PageId);
            page.NextPageId = freePageId;
            page.PrevPageId = uint.MaxValue;

            db.SetFreePageID(page.PageId);
            page.MarkDeleted();
        }

        public static void DropPages(DbCache db, IEnumerable<uint> pages)
        {
            foreach (var pageId in pages)
            {
                var page = db[pageId];

                if (page != null && !page.IsDeleted)
                {
                    RecyclePage(db, page);
                }
            }
        }

        public static BasePage Load(BinaryReader br, uint pageId)
        {
            PageType type;

            lock (br.BaseStream)
            {
                br.BaseStream.Seek(DbHeader.HEADER_SIZE + (long)pageId * BasePage.PAGE_SIZE, SeekOrigin.Begin);
                type = (PageType)br.ReadByte();
            }

            // using MemTest.MemChecker memChecker = new MemTest.MemChecker("load page");
            BasePage page = ReadPage(br, type, pageId);

            return page;
        }

        public static BasePage ReadPage(BinaryReader br, PageType type, uint pageId)
        {
            switch (type)
            {
                case PageType.Table:
                    {
                        var page = new TablePage();
                        page.Initialize(pageId);
                        page.Read(br);
                        return page;
                    }
                case PageType.Respository:
                    {
                        var page = new RepoPage();
                        page.Initialize(pageId);
                        page.Read(br);
                        return page;
                    }
                case PageType.Index:
                    {
                        var page = new IndexPage();
                        page.Initialize(pageId);
                        page.Read(br);
                        return page;
                    }
                case PageType.Data:
                    {
                        var page = new DataPage();
                        page.Initialize(pageId);
                        page.Read(br);
                        return page;
                    }
                case PageType.DataVar:
                    {
                        var page = new DataVarPage();
                        page.Initialize(pageId);
                        page.Read(br);
                        return page;
                    }
                default:
                    throw LumException.Raise($"unknown page type: {type}");
            }
        }
    }
}