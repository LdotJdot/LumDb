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
                new Span<byte>(pageBytes, PAGE_SIZE).Clear();

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

                var used = HEADER_SIZE;
                for (int i = 0; i < TotalDataCount; i++)
                {
                    var nodeBytes = DataVarNode.HEADER_SIZE + DataVarNodes[i].SpaceLength;
                    if (nodeBytes < DataVarNode.HEADER_SIZE || used > PAGE_SIZE - nodeBytes)
                        throw LumDbEngine.Element.Exceptions.LumException.Raise("dataVar node exceeds page");
                    used += nodeBytes;
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
            int bytesLeft = MAX_TOTAL_DATA_SIZE;
            for (int i = 0; i < TotalDataCount; i++)
            {
                DataVarNodes[i] = new DataVarNode(this);
                DataVarNodes[i].Read(br, bytesLeft);
                var taken = DataVarNodes[i].SpaceLength + DataVarNode.HEADER_SIZE;
                totalBytes += taken;
                bytesLeft -= taken;
            }

            // Stored RestPageSize used to ignore the 29-byte slot pad, so recompute it
            // from the nodes that actually fit on this page.
            RestPageSize = MAX_TOTAL_DATA_SIZE - totalBytes;
        }
    }
}