//global using TableHeaderInfo = (byte* keyName, LumDbEngine.Element.Structure.DbValueType type, bool isKey);
global using TableValue = (string columnName, object value);

namespace LumDbEngine.Element.Structure.Page.Key
{
    unsafe struct TableHeaderInfo
    {
        public byte* keyName;
        public DbValueType type;
        public bool isKey;
    }
    /// <summary>
    /// 38 bytes
    /// </summary>
    internal class ColumnHeader : IStructure
    {
        public const int NameLength = 32;
        public DbValueType ValueType { get; set; }      // 1 byte
        public bool IsKey { get; set; }                // 1 byte
        public byte[] Name { get; }        // 32 bytes,  39 bytes in total

        public NodeLink RootSubIndexNode;        // 5 bytes

        /// <summary>
        /// 38 bytes
        /// </summary>
        public ColumnHeader(TablePage page)
        {
            ValueType = DbValueType.Unknow;
            Name = new byte[NameLength];
            RootSubIndexNode = new NodeLink();
            IsKey = false;
            Page = page;
        }

        public TablePage Page { get; set; }
        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)ValueType);
            bw.Write(IsKey);
            bw.Write(Name);
            RootSubIndexNode.Write(bw);
        }

        public void Read(BinaryReader br)
        {
            ValueType = (DbValueType)br.ReadByte();
            IsKey = br.ReadBoolean();
            br.ReadBytes(NameLength).CopyTo(Name, 0);
            RootSubIndexNode.Read(br);
        }
    }
}