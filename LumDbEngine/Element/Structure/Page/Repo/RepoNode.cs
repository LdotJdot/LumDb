using LumDbEngine.Utils.ByteUtils;
using System.Text;

namespace LumDbEngine.Element.Structure.Page.Repo
{
    /// <summary>
    /// 47 bytes
    /// </summary>
    internal struct RepoNode : IStructure
    {
        public static RepoNode EmptyNode { get; set; }

        public const int Size = 48;

        public const int KeyLength = 32;

        public byte NodeIndex;          // 1 byte

        public NodeLink PrevKeyNodeLink = new NodeLink(); // 5 bytes

        public NodeLink NextKeyNodeLink = new NodeLink(); // 5 bytes

        public NodeLink TargetLink = new NodeLink();       // 5 bytes

        private ulong k1;
        private ulong k2;
        private ulong k3;
        private ulong k4;

        public void SetKey(in RepoNodeKey key)
        {
            k1 = key.k1;
            k2 = key.k2;
            k3 = key.k3;
            k4 = key.k4;
        }

        public bool IsKeyEqual(in RepoNodeKey key)
        {
            return
                   k1 == key.k1
                && k2 == key.k2
                && k3 == key.k3
                && k4 == key.k4;
        }

        internal string KeyToString()
        {
            Span<byte> spanBuffer = stackalloc byte[32];

            k1.SerializeObjectToBytes(spanBuffer.Slice(0, 8));
            k2.SerializeObjectToBytes(spanBuffer.Slice(8, 8));
            k3.SerializeObjectToBytes(spanBuffer.Slice(16, 8));
            k4.SerializeObjectToBytes(spanBuffer.Slice(24, 8));

            return Encoding.UTF8.GetString(spanBuffer);
        }

        // 47 bytes
        public RepoNode(uint pageId)
        {
            HostPageId = pageId;
            NodeIndex = byte.MaxValue;
        }

        public void Reset(uint pageId)
        {
            PrevKeyNodeLink.Reset();
            NextKeyNodeLink.Reset();
            TargetLink.Reset();
            k1 = 0;
            k2 = 0;
            k3 = 0;
            k4 = 0;
            HostPageId = pageId;
        }

        public uint HostPageId = uint.MaxValue;

        public void Write(BinaryWriter bw)
        {
            bw.Write(NodeIndex);
            PrevKeyNodeLink.Write(bw);
            NextKeyNodeLink.Write(bw);
            TargetLink.Write(bw);
            bw.Write(k1);
            bw.Write(k2);
            bw.Write(k3);
            bw.Write(k4);
        }

        public void Read(BinaryReader br)
        {
            NodeIndex = br.ReadByte();
            PrevKeyNodeLink.Read(br);
            NextKeyNodeLink.Read(br);
            TargetLink.Read(br);
            k1 = br.ReadUInt64();
            k2 = br.ReadUInt64();
            k3 = br.ReadUInt64();
            k4 = br.ReadUInt64();
        }
    }

    internal readonly struct RepoNodeKey
    {
        internal readonly ulong k1 = 0;
        internal readonly ulong k2 = 0;
        internal readonly ulong k3 = 0;
        internal readonly ulong k4 = 0;

        internal RepoNodeKey(Span<byte> bytes)
        {
            Span<byte> spanBuffer = stackalloc byte[8];
            var len = bytes.Length;
            if (len <= 8)
            {
                bytes.Slice(0).PaddingToBytes(spanBuffer, 8);
                k1 = BitConverter.ToUInt64(spanBuffer);
            }
            else
            {
                k1 = BitConverter.ToUInt64(bytes.Slice(0, 8));

                if (len <= 16)
                {
                    bytes.Slice(8).PaddingToBytes(spanBuffer, 8);
                    k2 = BitConverter.ToUInt64(spanBuffer);
                }
                else
                {
                    k2 = BitConverter.ToUInt64(bytes.Slice(8, 8));

                    if (len <= 24)
                    {
                        bytes.Slice(16).PaddingToBytes(spanBuffer, 8);
                        k3 = BitConverter.ToUInt64(spanBuffer);
                    }
                    else
                    {
                        k3 = BitConverter.ToUInt64(bytes.Slice(16, 8));

                        bytes.Slice(24).PaddingToBytes(spanBuffer, 8);
                        k4 = BitConverter.ToUInt64(spanBuffer);
                    }
                }
            }
        }
    }
}