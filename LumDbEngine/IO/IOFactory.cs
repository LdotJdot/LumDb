using LumDbEngine.Element.Exceptions;

namespace LumDbEngine.IO
{
    internal class IOFactory : IDisposable
    {
        private const int readerPoolSize = 5;

        private BinaryReaderPool readerPool;

        private Stream fileStream = null;
        private BinaryWriter binaryWriter = null;

        public BinaryReader RentReader()
        {
            LumException.ThrowIfNull(readerPool, "IO已释放");
            return readerPool.GetReader();
        }

        internal bool IsValid()
        {
            return !disposed;
        }

        public BinaryWriter BinaryWriter { get => binaryWriter; }
        public Stream FileStream { get => fileStream; }

        public IOFactory(string path)
        {
            this.fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            readerPool = new BinaryReaderPool(path, readerPoolSize);
            this.binaryWriter = new BinaryWriter(fileStream);
        }

        private bool disposed = false;

        public void Dispose()
        {
            if (disposed == false)
            {
                disposed = true;
                readerPool?.Dispose();
                binaryWriter?.Dispose();
                fileStream?.Dispose();
                readerPool = null;
                binaryWriter = null;
                fileStream = null;
            }
        }
    }
}