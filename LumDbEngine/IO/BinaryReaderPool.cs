using LumDbEngine.Element.Exceptions;
using System.Text;

namespace LumDbEngine.IO
{
    internal class BinaryReaderPool : IDisposable
    {
        private int poolSize;
        private BinaryReaderInPool[] buffers;

        public BinaryReaderPool(string path, int poolSize)
        {
            this.poolSize = poolSize;
            semaphore = new Semaphore(poolSize, poolSize);
            buffers = new BinaryReaderInPool[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                buffers[i] = new BinaryReaderInPool(fs, semaphore);
            }
        }

        private Semaphore semaphore;

        internal BinaryReader GetReader()
        {
            semaphore.WaitOne();

            // get the available buffer
            foreach (var buffer in buffers)
            {
                if (Interlocked.Increment(ref buffer.rented) == 1)
                {
                    return buffer;
                }
                else
                {
                    Interlocked.Decrement(ref buffer.rented);
                }
            }

            throw LumException.Raise("binaryReaderPool pv error");
        }

        public void Dispose()
        {
            foreach (var buffer in buffers)
            {
                buffer.Release();
            }
        }

        // using Release() method to dispose the reader actually
        internal class BinaryReaderInPool : BinaryReader
        {
            // the count of rented
            internal long rented = 0;

            private Semaphore semaphore;

            public BinaryReaderInPool(Stream input, Semaphore semaphore) : base(input)
            {
                this.semaphore = semaphore;
            }

            public BinaryReaderInPool(Stream input, Encoding encoding, Semaphore semaphore) : base(input, encoding)
            {
                this.semaphore = semaphore;
            }

            public BinaryReaderInPool(Stream input, Encoding encoding, bool leaveOpen, Semaphore semaphore) : base(input, encoding, leaveOpen)
            {
                this.semaphore = semaphore;
            }

            public override void Close()
            {
                Dispose(true);
            }

            protected override void Dispose(bool disposing)
            {
                if (Interlocked.Decrement(ref rented) == 0)
                {
                    semaphore.Release();
                }
                else
                {
                    Interlocked.Increment(ref rented);
                }
            }

            public void Release()
            {
                try
                {
                    BaseStream.Dispose();
                }
                catch
                {
                }
                base.Dispose(true);
            }
        }
    }
}