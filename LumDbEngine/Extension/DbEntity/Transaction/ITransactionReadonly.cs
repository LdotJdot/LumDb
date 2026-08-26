using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction.AsNoTracking
{
    public partial interface ITransactionReadonly : IDisposable
    {
        IDbValues<T> Find_Entity<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward = false) where T : IDbEntity, new();

        IDbValue<T> Find_Entity<T>(string tableName, string keyName, DbCell keyValue) where T : IDbEntity, new();

        IDbValue<T> Find_Entity<T>(string tableName, uint id) where T : IDbEntity, new();

        void GoThrough_Entity<T>(string tableName, Func<T, bool> action) where T : IDbEntity, new();
    }
}
