using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Utils.ByteUtils;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Structure.Page.Key
{
    internal class TablePage : BasePage
    {
        public override PageType Type { get; protected set; } = PageType.Table;

        public const int MAX_COLUMN_COUNT = 100;

        public const int HEADER_SIZE = 196;                                       // 4096 - 3900 = 196 bytes (13 + 34 + 4 bytes in use)

        public int ColumnCount;                // 4 bytes
        public TablePageHeader PageHeader;      // 34 bytes
        public ColumnHeader[] ColumnHeaders;    // maxSize is 39 * 100 = 3900 bytes

        public TablePage()
        {
        }

        internal override BasePage Initialize(uint pageID)
        {
            PageHeader = new TablePageHeader(this);
            InitializeColumnHeaders([]);
            base.Initialize(pageID);
            return this;
        }

        public void SetRootDataPageId(uint pageId)
        {
            PageHeader.RootDataPageId = pageId;
            MarkDirty();
        }

        private void InitializeColumnHeaders(ColumnHeader[] tableHeaders)
        {
            headerMap.Clear();
            for (int i = 0; i < tableHeaders.Length; i++)
            {
                headerMap.Add(tableHeaders[i].Name.TransformToToString(), i);
            }
            ColumnHeaders = tableHeaders;
            ColumnCount = tableHeaders.Length;
        }

        public void SetColumnHeaders(ColumnHeader[] tableHeaders)
        {
            InitializeColumnHeaders(tableHeaders);
            MarkDirty();
        }

        public int GetTableHeaderIndex(string headerName)
        {
            if (headerMap.TryGetValue(headerName, out var header))
            {
                return header;
            }
            else
            {
                throw LumException.Raise($"Column not found: {headerName}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetNextDataIdAndAutoIncrement()
        {
            MarkDirty();
            return ++PageHeader.NextDataId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRootMainIndexPage(BasePage page, byte index)
        {
            MarkDirty();
            PageHeader.RootIndexNode.TargetPageID = page.PageId;
            PageHeader.RootIndexNode.TargetNodeIndex = index;
            //PageHeader.RootIndexNode.Page = page;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLastDataPageId(uint id)
        {
            MarkDirty();
            PageHeader.LastDataPageId = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAvailableDataPageId(uint id)
        {
            MarkDirty();
            PageHeader.AvailableDataPage = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAvailableRepoPageId(uint id)
        {
            MarkDirty();
            Debug.Assert(id != uint.MaxValue);
            PageHeader.AvailableRepoPage = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColumnCount(byte count)
        {
            MarkDirty();
            PageHeader.ColumnCount = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataLength(uint length)
        {
            MarkDirty();
            PageHeader.DataLength = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAvailableIndexPage(uint id)
        {
            MarkDirty();
            PageHeader.AvailableIndexPage = id;
        }

        private Dictionary<string, int> headerMap = new();

        public unsafe override void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                var pageBytes = stackalloc byte[PAGE_SIZE];
                using var ms = new FixedStackallocMemoryStream(pageBytes, PAGE_SIZE);
                using var tmpBw = new BinaryWriter(ms);
                {
                    BasePageWrite(tmpBw);

                    PageHeader.Write(tmpBw);

                    tmpBw.Write(ColumnCount);

                    tmpBw.BaseStream.Seek(HEADER_SIZE, SeekOrigin.Begin);

                    for (int i = 0; i < ColumnCount; i++)
                    {
                        ColumnHeaders[i].Write(tmpBw);
                    }

                    var pos = DbHeader.HEADER_SIZE + (long)PageId * PAGE_SIZE;
                    var endPos = pos + PAGE_SIZE;
                    if (bw.BaseStream.Length < endPos) bw.BaseStream.SetLength(endPos);

                }
                
                MoveToPageStart(bw.BaseStream);
                bw.Write(new Span<byte>(pageBytes, PAGE_SIZE));
            }
        }

        public override void Read(BinaryReader br)
        {
            lock (br.BaseStream)
            {
                BasePageRead(br);
                PageHeader.Read(br);
                ColumnCount = br.ReadInt32();

                MoveToPageHeaderSizeOffset(br.BaseStream, HEADER_SIZE);

                // read columnHeaders
                var columnHeaders = new ColumnHeader[ColumnCount];

                for (int i = 0; i < ColumnCount; i++)
                {
                    columnHeaders[i] = new ColumnHeader(this);
                    columnHeaders[i].Read(br);
                }
                InitializeColumnHeaders(columnHeaders);
            }
        }
    }
}