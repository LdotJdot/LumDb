using LumDbEngine.Utils.ByteUtils;
using System.Text;

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
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            

            sb.AppendLine("// base: 13 bytes");
            sb.AppendLine(base.ToString());
            
            sb.AppendLine("// info: 12 bytes");
            sb.Append($"{{RestPageSize:{RestPageSize},TotalDataCount:{TotalDataCount},CurrentDataCount:{CurrentDataCount}}}");

            
            sb.AppendLine("// nodes:");
            sb.AppendLine("[");
            int index = 0;
            int totalData=0;

            foreach(var nd in DataVarNodes)
            {
                sb.AppendLine(nd.ToString()+ "  // "+index.ToString() );
                totalData += 13 + nd.SpaceLength;
                index++;
            }
            sb.AppendLine("]");
            
            sb.AppendLine($"// total dataVarNodeSize:{totalData}");



            
            return sb.ToString();
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

        public unsafe override void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                var pageBytes = stackalloc byte[PAGE_SIZE];
                
                WriteBytes(pageBytes);
                MoveToPageStart(bw.BaseStream);
                bw.Write(new Span<byte>(pageBytes, PAGE_SIZE));
            }
        }

        public override unsafe void WriteBytes(byte* bytes)
        {
            using var ms = new FixedStackallocMemoryStream(bytes, PAGE_SIZE);
            using var tmpBw = new BinaryWriter(ms);
            {
                BasePageWrite(tmpBw);
                tmpBw.Write(RestPageSize);
                tmpBw.Write(TotalDataCount);
                tmpBw.Write(CurrentDataCount);

                tmpBw.BaseStream.Seek(HEADER_SIZE, SeekOrigin.Begin);

                for (int i = 0; i < TotalDataCount; i++)
                {
                    DataVarNodes[i].Write(tmpBw);
                }
            }
        }

        public override void Read(BinaryReader br)
        {

            BasePageRead(br);
            RestPageSize = br.ReadInt32();
            TotalDataCount = br.ReadInt32();
            CurrentDataCount = br.ReadInt32();

            MoveToPageHeaderSizeOffset(br.BaseStream, HEADER_SIZE);
            DataVarNodes = new DataVarNode[TotalDataCount];
            int totalBytes = 0;
            for (int i = 0; i < TotalDataCount; i++)
            {
                DataVarNodes[i] = new DataVarNode(this);
                DataVarNodes[i].Read(br);
                totalBytes += DataVarNodes[i].SpaceLength+DataVarNode.HEADER_SIZE;
            }

            // additional calculate due to bug
            RestPageSize = BasePage.PAGE_SIZE - HEADER_SIZE - totalBytes;
        }
    }
}