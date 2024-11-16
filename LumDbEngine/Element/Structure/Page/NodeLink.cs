namespace LumDbEngine.Element.Structure.Page
{
    /// <summary>
    /// Size 5 bytes
    /// </summary>
    internal struct NodeLink : IStructure
    {
        public const int Size = 5;

        public uint TargetPageID { get; set; }    // 4 byte, 5 bytes in total
        public byte TargetNodeIndex { get; set; }     // 1 byte

        /// <summary>
        /// 5 bytes in toatl
        /// </summary>
        public NodeLink()
        {
            TargetNodeIndex = 0;
            TargetPageID = uint.MaxValue;
        }

        public void Reset()
        {
            TargetNodeIndex = 0;
            TargetPageID = uint.MaxValue;
        }

        public Span<byte> ToBytesAndSpan(Span<byte> bts)
        {
            BitConverter.GetBytes(TargetPageID).CopyTo(bts);
            bts[4] = TargetNodeIndex;
            return bts;
        }

        public static void Create(Span<byte> bytes, out NodeLink node)
        {
            node = new NodeLink();
            node.TargetPageID = BitConverter.ToUInt32(bytes.Slice(0, 4));
            node.TargetNodeIndex = bytes[4];
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(TargetPageID);
            bw.Write(TargetNodeIndex);
        }

        public void Read(BinaryReader br)
        {
            TargetPageID = br.ReadUInt32();
            TargetNodeIndex = br.ReadByte();
        }
    }
}