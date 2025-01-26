using LumDbEngine.Utils.ByteUtils;
using System.Reflection.PortableExecutable;

namespace LumDbEngine.Element.Structure
{
    /// <summary>
    /// Common db header to store the basic page information.
    /// </summary>
    internal class DbHeader : IStructure
    {
        // not in use
        public const int LOCKER_POS = 96;


        public const int STATE_POS = 88;

        // header size 100 bytes
        public const int HEADER_SIZE = 100;

        public const uint ROOT_INDEX = 0;

        private const int IDLENGTH = 6;

        private static readonly byte[] FileID = [76, 117, 109, 68, 66, 83];        // 6 bytes "LumDBS"

        public const uint VERSION = 1_001_002;



        public uint FileVersion { get; set; }           // 4 bytes
        public byte[] DbID { get; set; }

        /// <summary>
        /// root table id
        /// </summary>
        public uint RootTableRepoPage { get; set; }   // 4 bytes

        public uint AvailableTableRepoPage { get; set; }      // 4 bytes

        public uint AvailableDataVarPage { get; set; }      // 4 bytes

        public uint FreePage { get; set; }           // 4 bytes

        public uint LastPage { get; set; }           // 4 bytes

        // ready(0),writing(1)
        public byte State { get; set; }           // 4 bytes, 34 bytes in total

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

        public unsafe void Write(BinaryWriter bw)
        {
            lock (bw.BaseStream)
            {
                bw.Seek(0, SeekOrigin.Begin);
                var pageBytes = stackalloc byte[HEADER_SIZE];
                WriteBytes(pageBytes);
                bw.Write(new Span<byte>(pageBytes, HEADER_SIZE));
            }
        }
       
        public unsafe void WriteBytes(byte* bytes)
        {
            using var ms = new FixedStackallocMemoryStream(bytes, HEADER_SIZE);
            using var tmpBw = new BinaryWriter(ms);
            {
                tmpBw.Write(DbID);                          //6
                tmpBw.Write(FileVersion);                   //4
                tmpBw.Write(RootTableRepoPage);             //4
                tmpBw.Write(AvailableTableRepoPage);        //4
                tmpBw.Write(AvailableDataVarPage);          //4
                tmpBw.Write(FreePage);                      //4
                tmpBw.Write(LastPage);                      //4

                tmpBw.Seek(STATE_POS, SeekOrigin.Begin);
                tmpBw.Write(State);                //4
            }
        }

        public void Read(BinaryReader br)
        {

            br.BaseStream.Seek(0, SeekOrigin.Begin);
            DbID = br.ReadBytes(IDLENGTH);
            FileVersion = br.ReadUInt32();
            RootTableRepoPage = br.ReadUInt32();
            AvailableTableRepoPage = br.ReadUInt32();
            AvailableDataVarPage = br.ReadUInt32();
            FreePage = br.ReadUInt32();
            LastPage = br.ReadUInt32();

            br.BaseStream.Seek(STATE_POS, SeekOrigin.Begin);
            State = br.ReadByte();
        }
    }
}