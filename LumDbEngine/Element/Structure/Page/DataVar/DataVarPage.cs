namespace LumDbEngine.Element.Structure.Page.Data
{
    internal class DataVarPage : BasePage
    {
        public override PageType Type { get; protected set; } = PageType.DataVar;

        public const int HEADER_SIZE = COMMON_HEADER_SIZE + 12 + 20;                                          // 45 bytes, (13 + 12 bytes in use + 20 backup), common page header + this header + rest

        public const int MAX_TOTAL_DATA_SIZE = BasePage.PAGE_SIZE - HEADER_SIZE;                              // 4051 - HEADER_SIZE(45)

        public int RestPageSize;                                                                 // 4 bytes

        public int TotalDataCount;                                                               // 4 bytes

        public int CurrentDataCount;                                                             // 4 bytes

        public DataVarNode[] DataVarNodes;

        public DataVarPage()
        {
        }

        internal override BasePage Initialize(uint pageID)
        {
            CurrentDataCount = 0;
            RestPageSize = MAX_TOTAL_DATA_SIZE;
            TotalDataCount = 0;
            DataVarNodes = [];
            base.Initialize(pageID);
            return this;
        }

        public override void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                BasePageWrite(bw);
                bw.Write(RestPageSize);
                bw.Write(TotalDataCount);
                bw.Write(CurrentDataCount);
                MoveToPageHeaderSizeOffset(bw.BaseStream, HEADER_SIZE);
                for (int i = 0; i < TotalDataCount; i++)
                {
                    DataVarNodes[i].Write(bw);
                }
            }
        }

        public override void Read(BinaryReader br)
        {
            lock (br.BaseStream)
            {
                BasePageRead(br);
                RestPageSize = br.ReadInt32();
                TotalDataCount = br.ReadInt32();
                CurrentDataCount = br.ReadInt32();

                MoveToPageHeaderSizeOffset(br.BaseStream, HEADER_SIZE);
                DataVarNodes = new DataVarNode[TotalDataCount];
                for (int i = 0; i < TotalDataCount; i++)
                {
                    DataVarNodes[i] = new DataVarNode(this);
                    DataVarNodes[i].Read(br);
                }
            }
        }
    }
}