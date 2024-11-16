namespace LumDbEngine.Element.Structure.Page.KeyIndex
{
    /// <summary>
    /// Size 24 bytes
    /// </summary>
    internal struct IndexNodeTmp : IStructure, IDisposable
    {
        public const int Size = 24;

        internal const ulong ROOT_ID = ulong.MaxValue / 2;

        public ulong Id = 0;             // 8 bytes

        public byte NodeIndex;       // 1 byte

        public NodeLink Left = new NodeLink();         // 5 bytes

        public NodeLink Right = new NodeLink();        // 5 bytes

        public NodeLink KeyLink = new NodeLink();      // 5 bytes, 32 bytes in total

        /// <summary>
        /// 24 bytes
        /// </summary>
        /// <param name="indexPage"></param>
        public IndexNodeTmp(IndexPageTmp indexPage)
        {
            NodeIndex = byte.MaxValue;

            Left = new NodeLink();
            Right = new NodeLink();
            KeyLink = new NodeLink();
            Page = indexPage;
        }

        public uint PageId;
        private IndexPageTmp page;
        public IndexPageTmp Page { get { return page; } set { page = value; PageId = page?.PageId ?? uint.MaxValue; } }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Id);
            bw.Write(NodeIndex);
            Left.Write(bw);
            Right.Write(bw);
            KeyLink.Write(bw);
        }

        public void Read(BinaryReader br)
        {
            Id = br.ReadUInt64();
            NodeIndex = br.ReadByte();
            Left.Read(br);
            Right.Read(br);
            KeyLink.Read(br);
        }

        public void Dispose()
        {
            page = null;
        }

        public void CopyExcludePage(in IndexNode node)
        {
            Id = node.Id;
            NodeIndex = node.NodeIndex;
            Left = node.Left;
            Right = node.Right;
            KeyLink = node.KeyLink;
            PageId = node.HostPageId;
        }
    }
}