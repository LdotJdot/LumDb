using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LumDbExplorer.LumExplorer
{
    public class DbExplorer:IDisposable
    {
        string path;
        IOFactory iof;
        DbCache dbCache;
        public DbExplorer(string path)
        {
            this.path = path;

            this.iof = new IOFactory(path);

            dbCache = new DbCache(iof, 50000, true);

            pageCount = dbCache.LastPage;
        }

        public uint pageCount { get; }

        internal string GetPage(uint pageId)
        {
            if (pageId > dbCache.LastPage)
            {
                return "error, exceeds max page;";
            }
            else
            {
                return PageManager.GetPage(dbCache, pageId).ToString();
            }
        }


        public void Dispose()
        {
            iof.Dispose();
        }
    }
}
