using LumDbEngine.Element.Exceptions;
using LumDbEngine.IO;
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

        /// <summary>
        /// On-disk page format: major * 1_000_000 + minor * 1_000 + patch.
        /// 1_003_009 is 1.3.9. This is the file layout, not the assembly or NuGet version.
        /// Change it only when the page layout itself changes.
        /// </summary>
        public const uint VERSION = 1_003_009;

        /// <summary>Format a packed page-format version (e.g. 1_003_009 → "1.3.9").</summary>
        public static string FormatVersion(uint version)
            => $"{version / 1_000_000}.{(version / 1000) % 1000}.{version % 1000}";




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
                IsDirty = false;
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

        /// <summary>
        /// Read and validate the header of an existing image. A file that is too short, does not
        /// start with the "LumDBS" signature, or was written by another page format is rejected
        /// here instead of being decoded as if it were a database.
        /// </summary>
        public void Read(BinaryReader br)
        {
            var stream = br.BaseStream;
            if (stream.Length < HEADER_SIZE)
            {
                throw LumException.Raise(
                    $"{LumExceptionMessage.InvalidDatabaseFile} Length is {stream.Length} bytes, expected at least {HEADER_SIZE}.");
            }

            stream.Seek(0, SeekOrigin.Begin);
            var dbId = br.ReadBytes(IDLENGTH);
            if (dbId.Length != IDLENGTH || !dbId.AsSpan().SequenceEqual(FileID))
            {
                throw LumException.Raise($"{LumExceptionMessage.InvalidDatabaseFile} Signature check failed.");
            }

            var fileVersion = br.ReadUInt32();
            if (fileVersion != VERSION)
            {
                throw LumException.Raise(
                    $"{LumExceptionMessage.UnsupportedFileVersion} File format {FormatVersion(fileVersion)}, this build writes and reads {FormatVersion(VERSION)}.");
            }

            DbID = dbId;
            FileVersion = fileVersion;
            RootTableRepoPage = br.ReadUInt32();
            AvailableTableRepoPage = br.ReadUInt32();
            AvailableDataVarPage = br.ReadUInt32();
            FreePage = br.ReadUInt32();
            LastPage = br.ReadUInt32();

            stream.Seek(STATE_POS, SeekOrigin.Begin);
            State = br.ReadByte();
            IsDirty = false;
        }

        /// <summary>
        /// Validate an image before the engine starts recovering logs or handing out transactions.
        /// </summary>
        internal static void ValidateImage(IOFactory iof)
        {
            using var reader = iof.RentReader();
            new DbHeader().Read(reader);
        }
    }
}