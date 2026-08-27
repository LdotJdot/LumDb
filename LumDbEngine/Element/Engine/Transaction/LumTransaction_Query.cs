using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Query;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        private QuerySession CreateQuerySession()
        {
            return new QuerySession
            {
                Check = CheckTransactionState,
                StartRead = () => LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds),
                StartWrite = () => LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds),
                Discard = Discard,
                Db = db,
                Manager = dbManager,
            };
        }

        public IDbQueryable<T> Query<T>(string tableName) where T : ILumEntity<T>, new()
        {
            return new DbQueryable<T>(CreateQuerySession(), tableName, id =>
            {
                var found = dbManager.FindEntityById<T>(db, tableName, id);
                return found.IsSuccess ? found.Value : default;
            });
        }

        public IDbQueryable<T> Query_Entity<T>(string tableName) where T : IDbEntity, new()
        {
            return new DbQueryable<T>(CreateQuerySession(), tableName, id =>
            {
                var found = dbManager.FindById_Entity<T>(db, tableName, id);
                return found.IsSuccess ? found.Value : default;
            });
        }

        public IDbValues<T> Find<T>(string tableName, Expression<Func<T, bool>> predicate, uint skip = 0, uint limit = uint.MaxValue, bool isBackward = false)
            where T : ILumEntity<T>, new()
        {
            var q = Query<T>(tableName).Where(predicate);
            if (isBackward)
                q.Reverse();
            if (skip != 0)
                q.Skip(skip > int.MaxValue ? int.MaxValue : (int)skip);
            if (limit != uint.MaxValue)
                q.Take(limit > int.MaxValue ? int.MaxValue : (int)limit);
            return q.ToValues();
        }

        public IDbValue<uint> Count<T>(string tableName, Expression<Func<T, bool>> predicate) where T : ILumEntity<T>, new()
        {
            var n = Query<T>(tableName).Where(predicate).Count();
            return new DbValue<uint>((uint)n);
        }

        public IDbValues<T> Find_Entity<T>(string tableName, Expression<Func<T, bool>> predicate, uint skip = 0, uint limit = uint.MaxValue, bool isBackward = false)
            where T : IDbEntity, new()
        {
            var q = Query_Entity<T>(tableName).Where(predicate);
            if (isBackward)
                q.Reverse();
            if (skip != 0)
                q.Skip(skip > int.MaxValue ? int.MaxValue : (int)skip);
            if (limit != uint.MaxValue)
                q.Take(limit > int.MaxValue ? int.MaxValue : (int)limit);
            return q.ToValues();
        }

        public IDbValue<uint> Delete<T>(string tableName, Expression<Func<T, bool>> predicate)
            where T : ILumEntity<T>, new()
        {
            var n = Query<T>(tableName).Where(predicate).Delete();
            return new DbValue<uint>((uint)n);
        }

        public IDbValue<uint> Delete_Entity<T>(string tableName, Expression<Func<T, bool>> predicate)
            where T : IDbEntity, new()
        {
            var n = Query_Entity<T>(tableName).Where(predicate).Delete();
            return new DbValue<uint>((uint)n);
        }
    }
}
