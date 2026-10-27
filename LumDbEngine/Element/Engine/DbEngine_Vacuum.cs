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
        /// Other transactions keep running. Vacuum publishes only when no commit has landed
        /// since its snapshot, and retries so those commits are included.
        /// The previous image stays in place until the replacement has been fully committed.
        /// A crash before that swap leaves the old database and its <c>.log</c> as they were.
        /// A thread that already holds a transaction cannot call this.
        /// </summary>
        public void Vacuum()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
            if (Gate.HoldsWriterOnCurrentThread || Gate.HoldsUpgradeableOnCurrentThread)
                throw LumException.Raise("Vacuum cannot run on a thread that already holds a transaction.");

            if (memory != null)
                VacuumMemory();
            else
                VacuumFile();
        }

        private void VacuumMemory()
        {
            while (true)
            {
                if (Volatile.Read(ref disposed) != 0)
                    throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
                if (!TrySnapshotMemory(out var bytes, out var generation))
                {
                    Thread.Sleep(1);
                    continue;
                }

                var image = BuildMemory(bytes);
                if (TryPublishMemory(image, generation))
                    return;
                Thread.Sleep(1);
            }
        }

        private void VacuumFile()
        {
            var temp = path + ".vacuum.tmp";
            var snap = path + ".vacuum.snap";
            var backup = path + ".vacuum.bak";
            try
            {
                while (true)
                {
                    if (Volatile.Read(ref disposed) != 0)
                        throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
                    if (!TrySnapshotFile(snap, out var generation))
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    try
                    {
                        BuildFile(snap, temp);
                    }
                    catch
                    {
                        DeleteIfExists(temp);
                        DeleteIfExists(temp + ".log");
                        throw;
                    }
                    finally
                    {
                        DeleteIfExists(snap);
                    }

                    if (TryPublishFile(temp, backup, generation))
                        return;

                    DeleteIfExists(temp);
                    DeleteIfExists(temp + ".log");
                    DeleteIfExists(backup);
                    Thread.Sleep(1);
                }
            }
            finally
            {
                DeleteIfExists(snap);
                DeleteIfExists(temp);
                DeleteIfExists(temp + ".log");
                DeleteIfExists(backup);
            }
        }

        private bool TrySnapshotMemory(out byte[] bytes, out long generation)
        {
            bytes = [];
            generation = 0;
            if (!TryEngineLock(write: false, out var lk))
                return false;

            using (lk)
            {
                if (Volatile.Read(ref disposed) != 0)
                    throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
                bytes = memory!.ToArray();
                generation = Interlocked.Read(ref commitGeneration);
                return true;
            }
        }

        private bool TrySnapshotFile(string snap, out long generation)
        {
            generation = 0;
            if (!TryEngineLock(write: false, out var lk))
                return false;

            using (lk)
            {
                if (Volatile.Read(ref disposed) != 0)
                    throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
                if (iof?.FileStream == null)
                    throw LumException.Raise("Wrong path");

                DbLog.PrepareForWrite(this, iof.FileStream);
                DeleteIfExists(snap);
                lock (iof.FileStream)
                {
                    iof.BinaryWriter?.Flush();
                    iof.FileStream.Flush();
                    var pos = iof.FileStream.Position;
                    iof.FileStream.Seek(0, SeekOrigin.Begin);
                    using (var dest = new FileStream(snap, FileMode.Create, FileAccess.Write, FileShare.Read))
                        iof.FileStream.CopyTo(dest);
                    iof.FileStream.Seek(pos, SeekOrigin.Begin);
                }

                generation = Interlocked.Read(ref commitGeneration);
                return true;
            }
        }

        private static byte[] BuildMemory(byte[] snap)
        {
            var buffer = new MemoryDbBuffer();
            if (snap.Length > 0)
                buffer.Write(0, snap, 0, snap.Length);

            using var snapIo = new IOFactory(buffer);
            var source = new DbCache(snapIo, DbCache.DEFAULT_CACHE_PAGES, true);
            try
            {
                using var dest = new DbEngine();
                using (var ts = dest.StartTransaction())
                {
                    VacuumCopy.Copy(source, ((LumTransaction)ts).TransactionDb);
                    ts.SaveChanges();
                }

                return dest.CommittedImage();
            }
            finally
            {
                source.Dispose();
            }
        }

        private static void BuildFile(string snap, string temp)
        {
            DeleteIfExists(temp);
            DeleteIfExists(temp + ".log");
            using var snapIo = new IOFactory(snap);
            var source = new DbCache(snapIo, DbCache.DEFAULT_CACHE_PAGES, true);
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
        }

        private bool TryPublishMemory(byte[] image, long generation)
        {
            if (!TryEngineLock(write: true, out var lk))
                return false;

            using (lk)
            {
                if (Volatile.Read(ref disposed) != 0)
                    throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
                if (Interlocked.Read(ref commitGeneration) != generation)
                    return false;

                var buffer = new MemoryDbBuffer();
                if (image.Length > 0)
                    buffer.Write(0, image, 0, image.Length);

                var next = new IOFactory(buffer);
                var previous = iof;
                memory = buffer;
                iof = next;
                previous?.Dispose();
                return true;
            }
        }

        private bool TryPublishFile(string temp, string backup, long generation)
        {
            if (!TryEngineLock(write: true, out var lk))
                return false;

            using (lk)
            {
                if (Volatile.Read(ref disposed) != 0)
                    throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
                if (Interlocked.Read(ref commitGeneration) != generation)
                    return false;

                var previous = iof;
                iof = null;
                previous?.Dispose();
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
                return true;
            }
        }

        private bool TryEngineLock(bool write, out LockTransaction? lk)
        {
            try
            {
                lk = write
                    ? LockTransaction.TryStartWrite(Gate, TimeoutMilliseconds)
                    : LockTransaction.TryStartUpgradeableRead(Gate, TimeoutMilliseconds);
                return true;
            }
            catch (LumException ex) when (ex.Message == LumExceptionMessage.TransactionTimeout)
            {
                lk = null;
                return false;
            }
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
