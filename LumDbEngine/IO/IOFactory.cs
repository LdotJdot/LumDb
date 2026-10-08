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

        internal bool IsMemory { get; }
        internal MemoryDbBuffer? Memory { get; }

        public IOFactory(string path)
        {
            this.fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            readerPool = new BinaryReaderPool(path, readerPoolSize);
            this.binaryWriter = new BinaryWriter(fileStream);
            IsMemory = false;
        }

        internal IOFactory(MemoryDbBuffer memory)
        {
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.fileStream = new MemoryCursorStream(memory, writable: true);
            readerPool = new BinaryReaderPool(memory, readerPoolSize);
            this.binaryWriter = new BinaryWriter(fileStream);
            IsMemory = true;
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