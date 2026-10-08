using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using System.Reflection.Metadata;

namespace LumDbEngine.Element.LogStructure
{
    public enum DbLogState : byte
    {
        NotExisted = 1,
        Done = 2,
        Writing = 3,
        Corrupted = 4
    }
    /// <summary>
    /// Common db header to store the basic page information.
    /// </summary>
    internal static class DbLogUtils
    {
        static internal DbLogState CheckLogState(string logFilePath)
        {
            try
            {
                if (!File.Exists(logFilePath))
                    return DbLogState.NotExisted;

                using var fs = new FileStream(logFilePath, new FileStreamOptions() { Access = FileAccess.Read, Share = FileShare.ReadWrite | FileShare.Delete, Mode = FileMode.Open });
                using BinaryReader br = new BinaryReader(fs);
                return (DbLogState)br.ReadUInt32();
            }
            catch (Exception)
            {
                return DbLogState.Corrupted;
            }
        }

        static internal DbLogState CheckLogState(DbLog dbLog)
        {
            return CheckLogState(dbLog.LogFilePath);
        }

        static internal void MarkDbState(Stream stream, DbLogState state)
        {
            lock (stream)
            {
                stream.Seek(DbHeader.STATE_POS, SeekOrigin.Begin);
                stream.WriteByte((byte)state);
                if (stream is FileStream file)
                    file.Flush(true);
                else
                    stream.Flush();
            }
        }

        static internal DbLogState CheckDbState(BinaryReader dbBr)
        {
            dbBr.BaseStream.Seek(DbHeader.STATE_POS, SeekOrigin.Begin);
            return (DbLogState)dbBr.ReadByte();
        }

        static internal FileStream Open(DbLog dbLog)
        {
            var fs = new FileStream(dbLog.LogFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            return fs;
        }

        static internal FileStream Create(DbLog dbLog, bool deleteIfExisted)
        {
            if (deleteIfExisted)
            {
                Delete(dbLog);
            }
            var fs = new FileStream(dbLog.LogFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            return fs;
        }

        static internal void Delete(DbLog dbLog)
        {
            if (File.Exists(dbLog.LogFilePath))
            {
                File.Delete(dbLog.LogFilePath);
            }
        }
    }
}