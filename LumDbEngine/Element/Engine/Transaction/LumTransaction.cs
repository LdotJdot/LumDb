using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.IO;
using System.Text;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction : ITransaction
    {
        private static IDbManager dbManager = new DbManager();
        private DbCache db;
        private ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly Checker checker;
        internal int PagesCount => db.pages.Count;

        internal string DbState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("*******************************************");
            sb.AppendLine("total: " + db.pages.Values.Count());
            sb.AppendLine("table: " + db.pages.Values.Count(o => o.Type == PageType.Table));
            sb.AppendLine("data: " + db.pages.Values.Count(o => o.Type == PageType.Data));
            sb.AppendLine("index: " + db.pages.Values.Count(o => o.Type == PageType.Index));
            sb.AppendLine("repo: " + db.pages.Values.Count(o => o.Type == PageType.Respository));
            sb.AppendLine("dataVar: " + db.pages.Values.Count(o => o.Type == PageType.DataVar));
            return sb.ToString();
        }

        private readonly IOFactory? iof;
        private readonly long cachePages;
        private readonly bool dynamicCache;

        internal LumTransaction(IOFactory? iof, in Checker check, long cachePages, bool dynamicCache)
        {
            this.checker=check;
            db = new DbCache(iof, cachePages, dynamicCache);
            this.iof = iof;
            this.dynamicCache = dynamicCache;
        }

        private void CheckTransactionState()
        {
            LumException.ThrowIfTrue(disposed, "the current transaction is disposed");
        }

        public void SaveChanges()
        {
            CheckTransactionState();
            using (var lk = LockTransaction.StartWrite(rwLock))
            {
                db.SaveChanges();
            }
        }

        internal void SaveAs(string path)
        {
            CheckTransactionState();
            using var lk = LockTransaction.StartWrite(rwLock);
            db.SaveChanges(path);
        }

        public void Discard()
        {
            CheckTransactionState();
            using (var lk = LockTransaction.StartWrite(rwLock))
            {
                db = new DbCache(iof, cachePages, dynamicCache);
            }
        }

        private bool disposed = false;

        public void Dispose()
        {
            if (disposed == false)
            {
                disposed = true;
                try
                {
                    using (var lk = LockTransaction.StartWrite(rwLock))
                    {
                        db?.Dispose();
                        db = null;
                    }
                    rwLock.Dispose();
                }
                catch (Exception ex)
                {
                    throw LumException.Raise(ex.Message);
                }
                finally
                {
                    checker.Dispose();
                }
            }
        }

        internal class Checker:IDisposable
        {
            private readonly static ThreadLocal<int> callCount = new ThreadLocal<int>(() => 0);
            private readonly AutoResetEvent autoResetEvent; // make sure the singularity of transaction

            public Checker(AutoResetEvent autoResetEvent)
            {                
                if (callCount.Value != 0)
                {
                    LumException.Throw("In a single thread, the previous transaction should be disposed before starting another one.");

                }

                callCount.Value++;
                this.autoResetEvent = autoResetEvent;
                autoResetEvent.WaitOne();
            }

            public void Dispose()
            {
                callCount.Value--;
                if (!autoResetEvent.SafeWaitHandle.IsClosed) autoResetEvent.Set();
            }
        }
    }
}