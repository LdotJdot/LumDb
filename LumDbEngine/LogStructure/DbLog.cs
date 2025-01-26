using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure.Page;

namespace LumDbEngine.Element.Structure
{
    /// <summary>
    /// Common db header to store the basic page information.
    /// </summary>
    internal class DbLog:IDisposable
    {
        // ready(0), writing(1)

        public string LogFilePath { get; set; }

        private DbLog(DbEngine dbEngine)
        {
            this.dbEngine= dbEngine;
           
        }

        DbEngine dbEngine;
        FileStream dbLogFileStream;

        BinaryWriter logBw;
        BinaryReader logBr;


        public static DbLog OpenLogToRecoveryDbEngine(DbEngine dbEngine)
        {
            var dblog = new DbLog(dbEngine);
            dblog.LogFilePath = dbEngine.Path + ".log";

            var state = DbLogUtils.CheckLogState(dblog);

            switch (state)
            {
                case DbLogState.NotExisted:
                    throw LumException.Raise("The database is corrupted, and the backup logs are lost.");
                case DbLogState.Done:
                    dblog.dbLogFileStream = DbLogUtils.Open(dblog);
                    break;
                case DbLogState.Writing:
                    throw LumException.Raise("The database is corrupted, and the backup logs are lost.");
                default:
                    throw LumException.Throw("Unknown state of Dblog");
            }

            dblog.logBw = new BinaryWriter(dblog.dbLogFileStream);
            dblog.logBr = new BinaryReader(dblog.dbLogFileStream);

            return dblog;
        }
        
        public static DbLog Create(DbEngine dbEngine)
        {
            var dblog = new DbLog(dbEngine);
            dblog.LogFilePath = dbEngine.Path + ".log";

            var state = DbLogUtils.CheckLogState(dblog);

            switch (state)
            {
                case DbLogState.NotExisted:
                    dblog.dbLogFileStream = DbLogUtils.Create(dblog, false);
                    break;
                case DbLogState.Done:
                    throw new Exception("Fatal error in log write");
                case DbLogState.Writing:
                case DbLogState.Corrupted:
                    dblog.dbLogFileStream = DbLogUtils.Create(dblog, true);
                    break;
                default:
                    throw LumException.Throw("Unknown state in open Dblog");
            }

            dblog.logBw = new BinaryWriter(dblog.dbLogFileStream);
            dblog.logBr = new BinaryReader(dblog.dbLogFileStream);

            return dblog;
        }

        public void WriteState(DbLogState state)
        {
            lock (logBw.BaseStream)
            {
                logBw.Seek(0, SeekOrigin.Begin);
                logBw.Write((uint)state);
            }
        }
           

        public unsafe void Write(BasePage basePage)
        {
            lock (logBw.BaseStream)
            {
                var pageBytes = stackalloc byte[BasePage.PAGE_SIZE];
                basePage.WriteBytes(pageBytes);

                //move, write, and lengthen
                var originLen = logBw.BaseStream.Length;
                logBw.Seek(0, SeekOrigin.End);
                logBw.Write(new Span<byte>(pageBytes, BasePage.PAGE_SIZE));
            }
        }
        
        public unsafe void Write(DbHeader dbHeader)
        {
            lock (logBw.BaseStream)
            {
                dbHeader.State=(byte)DbLogState.Writing;

                logBw.BaseStream.Seek(4, SeekOrigin.Begin);
                var pageBytes = stackalloc byte[DbHeader.HEADER_SIZE];
                dbHeader.WriteBytes(pageBytes);

                logBw.Seek(0, SeekOrigin.End);
                logBw.Write(new Span<byte>(pageBytes, DbHeader.HEADER_SIZE));
            }
        }

        public void Dispose()
        {
            logBw.Dispose();
            logBr.Dispose();
            dbLogFileStream.Dispose();
            DbLogUtils.Delete(this);
        }

                  

        public unsafe void DumpToDbEngine(Stream stream)
        {
            lock (stream)
            {
                // header write
                {
                    dbLogFileStream.Seek(4, SeekOrigin.Begin);
                    var headerBytes = stackalloc byte[DbHeader.HEADER_SIZE];
                    var sp = new Span<byte>(headerBytes, DbHeader.HEADER_SIZE);
                    dbLogFileStream.Read(sp);

                    stream.Seek(0,SeekOrigin.Begin);
                    stream.Write(sp);
                }

                // page write
                {
                    long pos = 4 + DbHeader.HEADER_SIZE;
                    var pageBytes = stackalloc byte[BasePage.PAGE_SIZE];
                    while (pos < dbLogFileStream.Length)
                    {
                        
                        dbLogFileStream.Seek(pos + 1, SeekOrigin.Begin);
                        var pageId = logBr.ReadUInt32();


                        var sp = new Span<byte>(pageBytes, BasePage.PAGE_SIZE);
                        sp.Clear();

                        dbLogFileStream.Seek(pos, SeekOrigin.Begin);
                        dbLogFileStream.Read(sp);
                        BasePage.MoveToPageStart(stream, pageId);
                        stream.Write(sp);
                        pos += BasePage.PAGE_SIZE;
                    }
                }

                // state write
                {
                    stream.Seek(DbHeader.STATE_POS, SeekOrigin.Begin);
                    stream.WriteByte((byte)DbLogState.Done);
                    stream.Flush();
                }
            }
        }
    }
}