namespace LumDbEngine.Element.Structure.Page.Data
{
    internal class DataPage : BasePage
    {
        public override PageType Type { get; protected set; } = PageType.Data;

        public const int HEADER_SIZE = COMMON_HEADER_SIZE + 13 + 27;                // 53 bytes, (13 + 13 bytes in use + 31 backup), common page header + this header + rest

        public const int MAX_TOTAL_DATA_SIZE = BasePage.PAGE_SIZE - HEADER_SIZE;    // 4096 - HEADER_SIZE (53)

        public int MaxDataCount;                                       // 4 bytes
        public int DataLenthPerNode;                                   // 4 bytes

        public int CurrentDataCount;                                 // 4 bytes

        public byte AvailableNodeIndex;                                // 1 bytes, 13 bytes in total

        public DataNode[] DataNodes = null;

        public DataPage()
        {
        }

        public void ResetDataNodesSize()
        {
            DataNodes = new DataNode[MaxDataCount];
        }

        internal override BasePage Initialize(uint pageID)
        {
            MaxDataCount = 0;
            AvailableNodeIndex = 0;
            CurrentDataCount = 0;
            DataLenthPerNode = 0;
            base.Initialize(pageID);
            return this;
        }

        public override void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                BasePageWrite(bw);
                bw.Write(MaxDataCount);
                bw.Write(AvailableNodeIndex);
                bw.Write(CurrentDataCount);
                bw.Write(DataLenthPerNode);

                MoveToPageHeaderSizeOffset(bw.BaseStream, HEADER_SIZE);

                for (int i = 0; i < MaxDataCount; i++)
                {
                    DataNodes[i].Write(bw);
                }
            }
        }

        public override void Read(BinaryReader br)
        {
            lock (br.BaseStream)
            {
                BasePageRead(br);
                MaxDataCount = br.ReadInt32();
                AvailableNodeIndex = br.ReadByte();
                CurrentDataCount = br.ReadInt32();
                DataLenthPerNode = br.ReadInt32();

                MoveToPageHeaderSizeOffset(br.BaseStream, HEADER_SIZE);

                ResetDataNodesSize();

                for (int i = 0; i < MaxDataCount; i++)
                {
                    DataNodes[i] = new DataNode(PageId, DataLenthPerNode);
                    DataNodes[i].Read(br);
                }
            }
        }
    }
}