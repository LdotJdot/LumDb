namespace LumDbEngine.Element.Structure.Page.Data
{
    /// <summary>
    /// max less than 3200 + 24 bytes
    /// </summary>
    internal class DataNode : IStructure
    {
        public const int HEADER_SIZE = 20;              // 20 bytes

        public uint Id;                   // 8 byte

        public byte NodeIndex;             // 4 byte

        public byte NextFreeNodeIndex;     // 4 byte

        public bool IsAvailable;           // 4 bytes

        private readonly int DataLengthPerNode;
        public Span<byte> Data => data.AsSpan(0, DataLengthPerNode);               // less than 3200 bytes

        private byte[] data;                // dataLength, which is less than 3200 bytes

        /// max less than 3200 + 24 bytes
        public DataNode(uint pageId, int dataLength)
        {
            NodeIndex = byte.MaxValue;
            NextFreeNodeIndex = byte.MaxValue;
            this.data = new byte[dataLength];
            IsAvailable = false;
            Id = uint.MaxValue;
            HostPageId = pageId;
            DataLengthPerNode = dataLength;
        }

        public uint HostPageId = uint.MaxValue;

        public void Write(BinaryWriter bw)
        {
            bw.Write(Id);
            bw.Write(NodeIndex);
            bw.Write(NextFreeNodeIndex);
            bw.Write(IsAvailable);
            bw.Write(Data);
        }

        public void Read(BinaryReader br)
        {
            Id = br.ReadUInt32();
            NodeIndex = br.ReadByte();
            NextFreeNodeIndex = br.ReadByte();
            IsAvailable = br.ReadBoolean();
            br.ReadBytes(DataLengthPerNode).CopyTo(Data);
        }
    }
}