using LumDbEngine.Element.Exceptions;
using System.Diagnostics;

namespace LumDbEngine.Element.Structure.Page.KeyIndex
{
    internal class IndexPage : BasePage
    {
        public const byte ROOT_NODE = 0;
        public override PageType Type { get; protected set; } = PageType.Index;

        public const int HEADER_SIZE = 64;               // 35, 4096 - 4032 = 64 bytes (13+1 bytes in use), common page header + this header + rest

        public const int NODES_PER_PAGE = 168;                               // must be less than byte.MaxValue, 24 * 168 = 4032 bytes

        public byte AvailableNodeIndex;                      // 1 byte, 1 bytes in total page header

        public IndexNode[] Nodes;                              // 24 bytes per node

        public IndexPage()
        { }

        internal override BasePage Initialize(uint pageID)
        {
            base.Initialize(pageID);

            AvailableNodeIndex = 0;

            //using MemTest.MemChecker mc = new MemTest.MemChecker("NODES ARRAY RENT");

            Nodes = new IndexNode[NODES_PER_PAGE];
            //Nodes = BufferPool.ArrayPool_IndexNode.Rent(NODES_PER_PAGE);
            //Nodes= pool.Rent(NODES_PER_PAGE);
            //mc.Dispose();

            //using MemTest.MemChecker mc2 = new MemTest.MemChecker("indexNode create");
            for (byte i = 0; i < NODES_PER_PAGE; i++)
            {
                Nodes[i].Initialize(pageID); // set the next Available node to be the neibour
                Nodes[i].NodeIndex = i;
            }
            //mc2.Dispose();
            return this;
        }

        public bool IsFree()
        {
            return AvailableNodeIndex < NODES_PER_PAGE;
        }

        public override void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                BasePageWrite(bw);
                bw.Write(AvailableNodeIndex);

                MoveToPageHeaderSizeOffset(bw.BaseStream, HEADER_SIZE);
                for (int i = 0; i < NODES_PER_PAGE; i++)
                {
                    LumException.ThrowIfNotTrue(Nodes[i].NodeIndex == i, "page error");
                    Nodes[i].Write(bw);
                }
            }
        }

        public override void Read(BinaryReader br)
        {
            lock (br.BaseStream)
            {
                BasePageRead(br);
                AvailableNodeIndex = br.ReadByte();

                MoveToPageHeaderSizeOffset(br.BaseStream, HEADER_SIZE);
                for (int i = 0; i < NODES_PER_PAGE; i++)
                {
                    Nodes[i].Read(br);

                    Debug.Assert(Nodes[i].NodeIndex == i);
                }
            }
        }
    }
}