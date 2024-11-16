namespace LumDbEngine.Element.Structure.Page.Repo
{
    /// <summary>
    /// 47 bytes
    /// </summary>
    internal class RepoNodeTmp : IStructure, IDisposable
    {
        public const int Size = 48;

        public const int KeyLength = 32;

        public byte NodeIndex;          // 1 byte

        public NodeLink PrevKeyNodeLink; // 5 bytes

        public NodeLink NextKeyNodeLink; // 5 bytes

        public NodeLink TargetLink;       // 5 bytes

        //public Span<byte> Key => key.AsSpan(0, KeyLength);             // 32 bytes, 47 bytes in total

        //private byte[] key;             // 32 bytes, 47 bytes in total
        private long key1;

        private long key2;
        private long key3;
        private long key4;
        public static long count;

        // 47 bytes
        public RepoNodeTmp(RepoPageTmp keyPage)
        {
            PrevKeyNodeLink = new NodeLink();
            NextKeyNodeLink = new NodeLink();
            TargetLink = new NodeLink();
            Page = keyPage;
            NodeIndex = byte.MaxValue;

            //key = new byte[KeyLength];
            // key = new UnmanagedBytes(KeyLength);
            //key.Dispose();

            //key?.Dispose();
            //key = BufferPool.Byte32Pool.Rent(KeyLength);
            //key = new List<byte>(KeyLength);
            //BufferPool.Byte32Pool.Return(key, true);
            //Interlocked.Increment(ref count);
            //Console.WriteLine(key.Length);
        }

        public RepoPageTmp Page { get; set; }
        private static readonly byte[] EmptyKey = Enumerable.Repeat<byte>(0, KeyLength).ToArray();

        public void Write(BinaryWriter bw)
        {
            bw.Write(NodeIndex);
            PrevKeyNodeLink.Write(bw);
            NextKeyNodeLink.Write(bw);
            TargetLink.Write(bw);
        }

        public void Read(BinaryReader br)
        {
            NodeIndex = br.ReadByte();
            PrevKeyNodeLink.Read(br);
            NextKeyNodeLink.Read(br);
            TargetLink.Read(br);
        }

        public void Dispose()
        {
            //BufferPool.Byte32Pool.Return(key, true);
            Interlocked.Decrement(ref count);

            //BufferPool.Byte32Pool.Return(key,true);
            //key.Dispose();
        }
    }
}