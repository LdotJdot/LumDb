using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Engine.Cache
{
    internal partial class DbCache
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetHeaderDirty()
        {
            header.MarkDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetAvailableTableRepoId(uint id)
        {
            header.AvailableTableRepoPage = id;
            SetHeaderDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetAvailableDataVarPage(uint id)
        {
            header.AvailableDataVarPage = id;
            SetHeaderDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetFreePageID(uint id)
        {
            header.FreePage = id;
            SetHeaderDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetRootPageID(uint id)
        {
            header.RootTableRepoPage = id;
            SetHeaderDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetLastPageID(uint id)
        {
            header.LastPage = id;
            SetHeaderDirty();
        }

        public uint RootPageId
        {
            get
            {
                return header.RootTableRepoPage;
            }
        }

        public uint AvailableTableRepo
        {
            get
            {
                return header.AvailableTableRepoPage;
            }
        }

        public uint AvailableDataVarPage
        {
            get
            {
                return header.AvailableDataVarPage;
            }
        }

        public uint FreePage
        {
            get
            {
                return header.FreePage;
            }
        }

        public uint LastPage
        {
            get
            {
                return header.LastPage;
            }
        }
    }
}