using System.Text;

namespace LumDbEngine.Element.Structure.Page.Data
{
    /// <summary>
    /// 9 + x bytes
    /// </summary>
    internal class DataVarNode : IStructure
    {
        public const int HEADER_SIZE = 13;                            //9 bytes

        public const int REDUNDANCY_SIZE = HEADER_SIZE + 16;          //29 bytes

        public int TotalDataRestLength;                      // 4 bytes
        public int DataLength;                          // 4 bytes

        /// <summary>
        /// The actual byte array length of data in store.
        /// </summary>
        public int SpaceLength;                          // 4 bytes
        public bool IsAvailable;                         // 1 byte

        public Span<byte> Data => data.AsSpan(0, SpaceLength);

        private byte[] data;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"TotalDataRestLength:{TotalDataRestLength},datalength:{DataLength},spaceLength:{SpaceLength},isAvailable:{IsAvailable},dataLength:{data?.Length??0}");
            sb.Append("}");
            return sb.ToString();
        }

        public DataVarNode(DataVarPage Page)
        {
            this.Page = Page;
            this.data = null;
            this.TotalDataRestLength = 0;
            this.DataLength = 0;
            this.SpaceLength = 0;
            IsAvailable = false;
        }

        public void InitializeData()
        {
            //if(data != null)
            //{
            //    BufferPool.ByteVarPool.Return(data, true);
            //}

            //data = BufferPool.ByteVarPool.Rent(SpaceLength);
            data = new byte[SpaceLength];

            //Data=new byte[length];
        }

        public DataVarPage Page { get; set; }

        public void Write(BinaryWriter bw)
        {
            bw.Write(TotalDataRestLength);
            bw.Write(DataLength);
            bw.Write(SpaceLength);
            bw.Write(IsAvailable);
            bw.Write(Data);
        }

        public void Read(BinaryReader br)
        {
            TotalDataRestLength = br.ReadInt32();
            DataLength = br.ReadInt32();
            SpaceLength = br.ReadInt32();
            IsAvailable = br.ReadBoolean();
            InitializeData();
            br.ReadBytes(SpaceLength).CopyTo(Data);
        }
    }
}