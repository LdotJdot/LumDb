using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction.AsNoTracking
{
    public partial interface ITransactionReadonly : IDisposable
    {
        IDbQueryable<T> Query_Entity<T>(string tableName) where T : IDbEntity, new();

        IDbValues<T> Find_Entity<T>(string tableName, Expression<Func<T, bool>> predicate, uint skip = 0, uint limit = uint.MaxValue, bool isBackward = false) where T : IDbEntity, new();

        IDbValue<T> Find_Entity<T>(string tableName, string keyName, DbCell keyValue) where T : IDbEntity, new();

        IDbValue<T> Find_Entity<T>(string tableName, uint id) where T : IDbEntity, new();

        void GoThrough_Entity<T>(string tableName, Func<T, bool> action) where T : IDbEntity, new();
    }
}
