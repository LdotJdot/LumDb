namespace LumDbEngine.Element.Structure
{
    /// <summary>
    /// Common db header to store the basic page information.
    /// </summary>
    internal class DbHeader : IStructure
    {
        // not in use
        public const uint LOCKER_POS = 98;

        // header size 100 bytes
        public const int HEADER_SIZE = 100;

        public const uint ROOT_INDEX = 0;

        private const int IDLENGTH = 6;

        private static readonly byte[] FileID = [76, 117, 109, 68, 66, 83];        // 6 bytes "LumDBS"

        public const uint VERSION = 1_000_008;


        public uint FileVersion { get; set; }           // 4 bytes
        public byte[] DbID { get; set; }

        /// <summary>
        /// root table id
        /// </summary>
        public uint RootTableRepoPage { get; set; }   // 4 bytes

        public uint AvailableTableRepoPage { get; set; }      // 4 bytes

        public uint AvailableDataVarPage { get; set; }      // 4 bytes

        public uint FreePage { get; set; }           // 4 bytes

        public uint LastPage { get; set; }           // 4 bytes, 30 bytes in total

        public DbHeader()
        {
            FileVersion = VERSION;
            RootTableRepoPage = uint.MaxValue;
            AvailableTableRepoPage = uint.MaxValue;
            AvailableDataVarPage = uint.MaxValue;
            FreePage = uint.MaxValue;
            LastPage = uint.MaxValue;
            IsDirty = false;
            DbID = new byte[IDLENGTH];
            FileID.CopyTo(DbID, 0);
        }

        public bool IsDirty { get; private set; }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                bw.Seek(0, SeekOrigin.Begin);
                bw.Write(DbID);
                bw.Write(FileVersion);
                bw.Write(RootTableRepoPage);
                bw.Write(AvailableTableRepoPage);
                bw.Write(AvailableDataVarPage);
                bw.Write(FreePage);
                bw.Write(LastPage);
            }
        }

        public void Read(BinaryReader br)
        {
            lock (br.BaseStream)
            {
                br.BaseStream.Seek(0, SeekOrigin.Begin);
                DbID = br.ReadBytes(IDLENGTH);
                FileVersion = br.ReadUInt32();
                RootTableRepoPage = br.ReadUInt32();
                AvailableTableRepoPage = br.ReadUInt32();
                AvailableDataVarPage = br.ReadUInt32();
                FreePage = br.ReadUInt32();
                LastPage = br.ReadUInt32();
            }
        }
    }
}