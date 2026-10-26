using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.LogStructure;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.IO;

namespace LumDbEngine.Element.Engine
{
    public partial class DbEngine
    {
        /// <summary>
        /// Rebuild the committed database so free pages and dead variable-length nodes are dropped
        /// and the file (or memory image) shrinks. Row ids, column order, and values are kept.
        /// There must be no open transaction.
        /// The previous image stays in place until the replacement has been fully committed.
        /// A crash before that swap leaves the old database and its <c>.log</c> as they were.
        /// </summary>
        public void Vacuum()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
            if (!transactionsPool.IsEmpty)
                throw LumException.Raise("Vacuum requires no open transaction.");

            using var lk = LockTransaction.TryStartWrite(ReadWriteLock, TimeoutMilliseconds);
            if (Volatile.Read(ref disposed) != 0)
                throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
            if (!transactionsPool.IsEmpty)
                throw LumException.Raise("Vacuum requires no open transaction.");

            if (memory != null)
                VacuumMemory();
            else
                VacuumFile();
        }

        private void VacuumMemory()
        {
            var source = new DbCache(iof, DbCache.DEFAULT_CACHE_PAGES, true);
            byte[] image;
            try
            {
                using var dest = new DbEngine();
                using (var ts = dest.StartTransaction())
                {
                    VacuumCopy.Copy(source, ((LumTransaction)ts).TransactionDb);
                    ts.SaveChanges();
                }

                image = dest.CommittedImage();
            }
            finally
            {
                source.Dispose();
            }

            var buffer = new MemoryDbBuffer();
            if (image.Length > 0)
                buffer.Write(0, image, 0, image.Length);

            var next = new IOFactory(buffer);
            var previous = iof;
            memory = buffer;
            iof = next;
            previous?.Dispose();
        }

        private void VacuumFile()
        {
            if (iof?.FileStream == null)
                throw LumException.Raise("Wrong path");

            // Fold a finished log into the main file, or delete an incomplete one,
            // before reading the image that will be rebuilt.
            DbLog.PrepareForWrite(this, iof.FileStream);

            var temp = path + ".vacuum.tmp";
            var backup = path + ".vacuum.bak";
            DeleteIfExists(temp);
            DeleteIfExists(temp + ".log");
            DeleteIfExists(backup);

            var source = new DbCache(iof, DbCache.DEFAULT_CACHE_PAGES, true);
            try
            {
                using var dest = new DbEngine(temp, true);
                using (var ts = dest.StartTransaction())
                {
                    VacuumCopy.Copy(source, ((LumTransaction)ts).TransactionDb);
                    ts.SaveChanges();
                }
            }
            finally
            {
                source.Dispose();
            }

            var previous = iof;
            iof = null;
            previous.Dispose();

            try
            {
                File.Replace(temp, path, backup, ignoreMetadataErrors: true);
            }
            catch
            {
                iof = new IOFactory(path);
                throw;
            }

            DeleteIfExists(backup);
            DeleteIfExists(temp + ".log");
            iof = new IOFactory(path);
        }

        internal byte[] CommittedImage()
        {
            if (memory == null)
                throw LumException.Raise("CommittedImage is only available for a memory engine.");
            return memory.ToArray();
        }

        private static void DeleteIfExists(string file)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
