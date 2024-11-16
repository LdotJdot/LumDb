namespace LumDbEngine.Element.Structure.Page.Repo
{
    /// <summary>
    /// respository page
    /// </summary>
    internal class RepoPage : BasePage
    {
        public override PageType Type { get; protected set; } = PageType.Respository;

        public const int HEADER_SIZE = 16;                                // 4096 - 4080 = 16 bytes (13 + 1 bytes in use)

        public const int NODES_PER_PAGE = 85;                              // 85 * 48   = 4032 bytes

        public byte AvailableNodeIndex;                       // 1 byte

        public RepoNode[] Nodes { get; set; }                           // 48 bytes per node

        public RepoPage()
        {
        }

        internal override BasePage Initialize(uint pageID)
        {
            AvailableNodeIndex = 0;

            Nodes = new RepoNode[NODES_PER_PAGE];
            //Nodes = BufferPool.ArrayPool_RepoNode.Rent(NODES_PER_PAGE);
            //Nodes = pool.Rent(NODES_PER_PAGE);

            for (int i = 0; i < NODES_PER_PAGE; i++)
            {
                Nodes[i].HostPageId = pageID;
                Nodes[i].NodeIndex = (byte)i;
            }
            base.Initialize(pageID);
            return this;
        }

        public bool HasAvailableNode()
        {
            return AvailableNodeIndex < NODES_PER_PAGE;
        }

        public override void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                BasePageWrite(bw);
                bw.Write(AvailableNodeIndex);
                MoveToPageHeaderSizeOffset(bw.BaseStream, HEADER_SIZE);

                for (int i = 0; i < NODES_PER_PAGE; i++)
                {
                    Nodes[i].Write(bw);
                }
            }
        }

        public override void Read(BinaryReader br)
        {
            lock (br.BaseStream)
            {
                BasePageRead(br);
                AvailableNodeIndex = br.ReadByte();
                MoveToPageHeaderSizeOffset(br.BaseStream, HEADER_SIZE);
                for (int i = 0; i < NODES_PER_PAGE; i++)
                {
                    Nodes[i].Read(br);
                    Nodes[i].HostPageId = PageId;
                }
            }
        }
    }
}