using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Query;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine
{
    internal sealed class DbJoinQueryable<T1, T2> : IDbJoinQueryable<T1, T2>
    {
        private readonly QueryOwner _owner = new();
        private readonly QuerySession _session;
        private readonly string _t0;
        private readonly string _t1;
        private readonly OuterSnap _outer;
        private readonly LambdaExpression _on;
        private readonly Func<uint, T2?> _from1;
        private readonly Func<uint, T1?> _from0;
        private readonly List<LambdaExpression> _wheres = new();
        private LambdaExpression? _orderBy;
        private bool _orderDescending;
        private int _skip;
        private int _take = int.MaxValue;

        internal DbJoinQueryable(
            QuerySession session,
            string t0,
            string t1,
            OuterSnap outer,
            LambdaExpression on,
            Func<uint, T2?> from1,
            Func<uint, T1?> from0)
        {
            _session = session;
            _t0 = t0;
            _t1 = t1;
            _outer = outer;
            _on = on;
            _from1 = from1;
            _from0 = from0;
        }

        public IDbJoinQueryable<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _owner.Check();
            {
                _wheres.Add(predicate);
                return this;
            }
        }

        public IDbJoinQueryable<T1, T2> OrderBy<TKey>(Expression<Func<T1, T2, TKey>> keySelector)
        {
            ArgumentNullException.ThrowIfNull(keySelector);
            _owner.Check();
            {
                if (_orderBy != null)
                    throw LumException.Raise(LumExceptionMessage.OrderByAlreadyDefined);
                _orderBy = keySelector;
                _orderDescending = false;
                return this;
            }
        }

        public IDbJoinQueryable<T1, T2> OrderByDescending<TKey>(Expression<Func<T1, T2, TKey>> keySelector)
        {
            ArgumentNullException.ThrowIfNull(keySelector);
            _owner.Check();
            {
                if (_orderBy != null)
                    throw LumException.Raise(LumExceptionMessage.OrderByAlreadyDefined);
                _orderBy = keySelector;
                _orderDescending = true;
                return this;
            }
        }

        public IDbJoinQueryable<T1, T2> Skip(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            _owner.Check();
            {
                _skip = count;
                return this;
            }
        }

        public IDbJoinQueryable<T1, T2> Take(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            _owner.Check();
            {
                _take = count;
                return this;
            }
        }

        public IDbJoin3Queryable<T1, T2, T3> Join<T3>(string innerTable, Expression<Func<T1, T2, T3, bool>> on)
            where T3 : ILumEntity<T3>, new()
        {
            ArgumentNullException.ThrowIfNull(on);
            _owner.Check();
            {
                return new DbJoin3Queryable<T1, T2, T3>(
                    _session, _t0, _t1, innerTable, _outer, _on, [.. _wheres], _skip, _take, _orderBy, _orderDescending, on,
                    _from0, _from1,
                    id =>
                    {
                        var found = _session.Manager.FindEntityById<T3>(_session.Db, innerTable, id);
                        return found.IsSuccess ? found.Value : default;
                    });
            }
        }

        public IDbJoin3Queryable<T1, T2, T3> JoinEntity<T3>(string innerTable, Expression<Func<T1, T2, T3, bool>> on)
            where T3 : IDbEntity, new()
        {
            ArgumentNullException.ThrowIfNull(on);
            _owner.Check();
            {
                return new DbJoin3Queryable<T1, T2, T3>(
                    _session, _t0, _t1, innerTable, _outer, _on, [.. _wheres], _skip, _take, _orderBy, _orderDescending, on,
                    _from0, _from1,
                    id =>
                    {
                        var found = _session.Manager.FindById_Entity<T3>(_session.Db, innerTable, id);
                        return found.IsSuccess ? found.Value : default;
                    });
            }
        }

        public IDbValues<(T1 Outer, T2 Inner)> ToValues()
        {
            _owner.Check();
                return new DbValues<(T1, T2)>(ExecutePairs(_take));
        }

        public List<(T1 Outer, T2 Inner)> ToList()
        {
            var values = ToValues();
            if (!values.IsSuccess)
                throw values.Exception ?? LumException.Raise("query failed");
            return values.Values.ToList();
        }

        public (T1 Outer, T2 Inner)[] ToArray() => ToList().ToArray();

        public (T1 Outer, T2 Inner) First()
        {
            _owner.Check();
            {
                var list = ExecutePairs(Math.Min(_take, 1));
                if (list.Count == 0)
                    throw new InvalidOperationException("Sequence contains no elements");
                return list[0];
            }
        }

        public (T1 Outer, T2 Inner)? FirstOrDefault()
        {
            _owner.Check();
            {
                var list = ExecutePairs(Math.Min(_take, 1));
                return list.Count == 0 ? null : list[0];
            }
        }

        public int Count()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return RelOpExecutor.Execute(_session.Db, BuildRelOp(_take)).Count;
            }
        }

        public bool Exists()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return RelOpExecutor.Execute(_session.Db, BuildRelOp(Math.Min(_take, 1))).Count > 0;
            }
        }

        private List<(T1 Outer, T2 Inner)> ExecutePairs(int take)
        {
            _session.Check();
            using var lk = _session.StartRead();
            var tuples = RelOpExecutor.Execute(_session.Db, BuildRelOp(take));
            var list = new List<(T1, T2)>(tuples.Count);
            foreach (var t in tuples)
            {
                if (t.Length < 2)
                    continue;
                var a = _from0(t[0]);
                var b = _from1(t[1]);
                if (a != null && b != null)
                    list.Add((a, b));
            }

            return list;
        }

        private RelOp BuildRelOp(int take)
            => RelOpFactory.TwoTable(_session.Db, _t0, _t1, _outer, _on, _wheres, _skip, take, _orderBy, _orderDescending);
    }
}
