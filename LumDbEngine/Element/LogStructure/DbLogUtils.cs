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
        static internal DbLogState CheckLogState(DbLog dbLog)
        {
            try
            {

                if (!File.Exists(dbLog.LogFilePath))
                {
                    return DbLogState.NotExisted;
                }
                using var fs = new FileStream(dbLog.LogFilePath, new FileStreamOptions() { Access = FileAccess.Read, Share = FileShare.ReadWrite | FileShare.Delete, Mode = FileMode.Open });
                using BinaryReader br = new BinaryReader(fs);
                return (DbLogState)br.ReadUInt32();
            }
            catch (Exception ex)
            {
                return DbLogState.Corrupted;
            }
        }

        static internal DbLogState CheckDbState(BinaryReader dbBr)
        {
            dbBr.BaseStream.Seek(DbHeader.STATE_POS, SeekOrigin.Begin);
            return (DbLogState)dbBr.ReadByte();
        }

        static internal FileStream Open(DbLog dbLog)
        {
            var fs = new FileStream(dbLog.LogFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            return fs;
        }

        static internal FileStream Create(DbLog dbLog, bool deleteIfExisted)
        {
            if (deleteIfExisted)
            {
                Delete(dbLog);
            }
            var fs = new FileStream(dbLog.LogFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
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