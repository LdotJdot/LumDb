using LumDbEngine.Element.Exceptions;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Structure.Page
{
    internal enum PageType : byte
    {
        Blank = 0,
        Respository = 1,
        Table = 2,
        Index = 3,
        Data = 5,
        DataVar = 6,
    }

    internal abstract class BasePage : IStructure
    {
        public const int PAGE_SIZE = 4096;          // 4 kb in total (buffer cache size)

        public const int COMMON_HEADER_SIZE = 13;  // 公共页面头部长度为 7 bytes

        public abstract PageType Type { get; protected set; }      // 1 bytes

        public uint PageId;            // 4 bytes

        /// <summary>
        ///  next page with same pageType
        /// </summary>

        public uint NextPageId;        // 4 bytes

        public uint PrevPageId;       // 4 bytes, 13 bytes in total

        //static long pageCount = 0;
        internal BasePage()
        {
            //Console.WriteLine($"Total {Interlocked.Increment(ref pageCount)} ");
        }

        internal virtual BasePage Initialize(uint pageId)
        {
            IsDirty = false;
            PageId = pageId;
            NextPageId = uint.MaxValue;
            PrevPageId = uint.MaxValue;
            return this;
        }

        internal virtual void Reset()
        {
            IsDirty = false;
            NextPageId = uint.MaxValue;
            PrevPageId = uint.MaxValue;
        }

        public bool IsDirty;
        public bool IsDeleted;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkDirty()
        {
            IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkDeleted()
        {
            IsDeleted = true;
        }

        public void CleanDelete()
        {
            IsDeleted = false;
        }

        public abstract void Write(BinaryWriter bw);

        public abstract void Read(BinaryReader br);

        protected void BasePageWrite(BinaryWriter bw)
        {           
            bw.Write((byte)Type);
            bw.Write(PageId);
            bw.Write(NextPageId);
            bw.Write(PrevPageId);
        }

        protected void MoveToPageHeaderSizeOffset(Stream stream, int headerSize)
        {
            var pos = DbHeader.HEADER_SIZE + (long)PageId * PAGE_SIZE + headerSize;
            stream.Seek(pos, SeekOrigin.Begin);
        }
             
        protected void MoveToPageStart(Stream stream)
        {
            var pos = DbHeader.HEADER_SIZE + (long)PageId * PAGE_SIZE;
            var endPos = pos + PAGE_SIZE;
            if (stream.Length < endPos) stream.SetLength(endPos);

            stream.Seek(pos, SeekOrigin.Begin);
        }

        protected void BasePageRead(BinaryReader br)
        {
            br.BaseStream.Seek(DbHeader.HEADER_SIZE + (long)PageId * PAGE_SIZE, SeekOrigin.Begin);
            LumException.ThrowIfNotTrue(Type == (PageType)br.ReadByte(), "page error");  // value of PageType should be the same with the data on disk.
            LumException.ThrowIfNotTrue(PageId == br.ReadUInt32(), "page error");       // value of PageID should be the same with the data on disk.
            NextPageId = br.ReadUInt32();
            PrevPageId = br.ReadUInt32();
        }

        //private bool disposedValue;

        //protected abstract void Release();

        //private void Dispose(bool disposing)
        //{
        //    if (!disposedValue)
        //    {
        //        if (disposing)
        //        {
        //            // TODO: 释放托管状态(托管对象)
        //        }

        //        Release();
        //        // TODO: 释放未托管的资源(未托管的对象)并重写终结器
        //        // TODO: 将大型字段设置为 null
        //        disposedValue = true;
        //    }
        //}

        //// // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        //~BasePage()
        //{
        //    // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //    Dispose(disposing: false);
        //}

        //public void Dispose()
        //{
        //    // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //    Dispose(disposing: true);
        //    GC.SuppressFinalize(this);
        //}

        //// public abstract void Dispose();
    }
}