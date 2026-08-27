using System.Linq.Expressions;
using LumDbEngine;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    public partial interface ITransaction
    {
        IDbResult Create<T>(string tableName) where T : ILumEntity<T>, new();

        IDbValue<uint> Insert<T>(string tableName, T value) where T : ILumEntity<T>, new();

        IDbValue<T> FindByIdEntity<T>(string tableName, uint id) where T : ILumEntity<T>, new();

        IDbValue<T> FindEntity<T>(string tableName, string keyName, DbCell keyValue) where T : ILumEntity<T>, new();

        IDbResult UpdateEntity<T>(string tableName, uint id, T value) where T : ILumEntity<T>, new();

        IDbResult UpdateEntity<T>(string tableName, string keyName, DbCell keyValue, T value) where T : ILumEntity<T>, new();

        IDbValues<T> Find<T>(string tableName, RowViewPredicate predicate, uint skip = 0, uint limit = uint.MaxValue)
            where T : ILumEntity<T>, new();

        IDbQueryable<T> Query<T>(string tableName) where T : ILumEntity<T>, new();

        IDbValues<T> Find<T>(string tableName, Expression<Func<T, bool>> predicate, uint skip = 0, uint limit = uint.MaxValue, bool isBackward = false)
            where T : ILumEntity<T>, new();

        IDbValue<uint> Count(string tableName, RowViewPredicate predicate);

        IDbValue<uint> Count<T>(string tableName, Expression<Func<T, bool>> predicate) where T : ILumEntity<T>, new();

        IDbValue<uint> Delete<T>(string tableName, Expression<Func<T, bool>> predicate) where T : ILumEntity<T>, new();
    }
}
