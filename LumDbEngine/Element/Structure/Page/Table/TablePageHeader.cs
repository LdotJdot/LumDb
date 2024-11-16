using LumDbEngine.Element.Structure.Page.Key;

namespace LumDbEngine.Element.Structure.Page.Table
{
    /// <summary>
    /// 37 bytes
    /// </summary>
    internal class TablePageHeader : IStructure
    {
        public const int HEADER_SIZE = 34;

        public TablePageHeader(TablePage page)
        {
            RootDataPageId = uint.MaxValue;
            ColumnCount = byte.MaxValue;
            LastDataPageId = uint.MaxValue;
            AvailableDataPage = uint.MaxValue;
            AvailableIndexPage = uint.MaxValue;
            AvailableRepoPage = uint.MaxValue;
            RootIndexNode = new NodeLink();
            NextDataId = 0;
            DataLength = 0;
        }

        public byte ColumnCount;                                       // 1 byte

        public uint DataLength;                                       // 4 bytes

        public uint RootDataPageId;                                       // 4 bytes

        public NodeLink RootIndexNode;                               // 5 bytes

        public uint AvailableDataPage;                                // 4 bytes

        public uint LastDataPageId;                                   // 4 bytes
        public uint NextDataId;                                      // 4 bytes

        public uint AvailableRepoPage;                             // 4 bytes

        public uint AvailableIndexPage;                               // 4 bytes, 34 bytes in total

        public void Write(BinaryWriter bw)
        {
            bw.Write(RootDataPageId);
            bw.Write(ColumnCount);
            bw.Write(LastDataPageId);
            bw.Write(AvailableDataPage);
            bw.Write(AvailableIndexPage);
            bw.Write(AvailableRepoPage);
            bw.Write(NextDataId);
            bw.Write(DataLength);
            RootIndexNode.Write(bw);
        }

        public void Read(BinaryReader br)
        {
            RootDataPageId = br.ReadUInt32();
            ColumnCount = br.ReadByte();
            LastDataPageId = br.ReadUInt32();
            AvailableDataPage = br.ReadUInt32();
            AvailableIndexPage = br.ReadUInt32();
            AvailableRepoPage = br.ReadUInt32();
            NextDataId = br.ReadUInt32();
            DataLength = br.ReadUInt32();
            RootIndexNode.Read(br);
        }
    }
}