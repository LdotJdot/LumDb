using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Query;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine
{
    internal sealed class DbQueryable<T> : IDbQueryable<T>
    {
        private readonly QueryOwner _owner = new();
        private readonly QuerySession _session;
        private readonly string _table;
        private readonly Func<uint, T?> _fromId;
        private readonly List<LambdaExpression> _wheres = new();
        private readonly List<RowExpr> _extra = new();
        private readonly List<(string Table, LambdaExpression On)> _exists = new();
        private LambdaExpression? _orderBy;
        private bool _orderDescending;
        private int _skip;
        private int _take = int.MaxValue;
        private bool _backward;

        internal DbQueryable(QuerySession session, string table, Func<uint, T?> fromId)
        {
            _session = session;
            _table = table;
            _fromId = fromId;
        }

        public IDbQueryable<T> Where(Expression<Func<T, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _owner.Check();
            {
                _wheres.Add(predicate);
                return this;
            }
        }

        public IDbQueryable<T> WhereCallback(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _owner.Check();
            {
                _extra.Add(new EntityPredExpr<T>(_fromId, predicate));
                return this;
            }
        }

        public IDbQueryable<T> Where(RowViewPredicate predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _owner.Check();
            {
                _extra.Add(new RowPredExpr(predicate));
                return this;
            }
        }

        public IDbQueryable<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
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

        public IDbQueryable<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
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

        public IDbQueryable<T> Skip(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            _owner.Check();
            {
                _skip = count;
                return this;
            }
        }

        public IDbQueryable<T> Take(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            _owner.Check();
            {
                _take = count;
                return this;
            }
        }

        public IDbQueryable<T> Reverse()
        {
            _owner.Check();
            {
                _backward = true;
                return this;
            }
        }

        public IDbJoinQueryable<T, TInner> Join<TInner>(string innerTable, Expression<Func<T, TInner, bool>> on)
            where TInner : ILumEntity<TInner>, new()
        {
            ArgumentNullException.ThrowIfNull(on);
            _owner.Check();
            {
                return new DbJoinQueryable<T, TInner>(
                    _session, _table, innerTable, Snapshot(), on,
                    id =>
                    {
                        var found = _session.Manager.FindEntityById<TInner>(_session.Db, innerTable, id);
                        return found.IsSuccess ? found.Value : default;
                    },
                    _fromId);
            }
        }

        public IDbJoinQueryable<T, TInner> JoinEntity<TInner>(string innerTable, Expression<Func<T, TInner, bool>> on)
            where TInner : IDbEntity, new()
        {
            ArgumentNullException.ThrowIfNull(on);
            _owner.Check();
            {
                return new DbJoinQueryable<T, TInner>(
                    _session, _table, innerTable, Snapshot(), on,
                    id =>
                    {
                        var found = _session.Manager.FindById_Entity<TInner>(_session.Db, innerTable, id);
                        return found.IsSuccess ? found.Value : default;
                    },
                    _fromId);
            }
        }

        public IDbQueryable<T> WhereExists<TInner>(string innerTable, Expression<Func<T, TInner, bool>> on)
        {
            ArgumentNullException.ThrowIfNull(on);
            _owner.Check();
            {
                _exists.Add((innerTable, on));
                return this;
            }
        }

        public IDbValues<T> ToValues()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return new DbValues<T>(Materialize(ExecuteIdsUnlocked(_take)));
            }
        }

        public List<T> ToList()
        {
            var values = ToValues();
            if (!values.IsSuccess)
                throw values.Exception ?? LumException.Raise("query failed");
            return values.Values.ToList();
        }

        public T[] ToArray() => ToList().ToArray();

        public T First()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                var list = Materialize(ExecuteIdsUnlocked(Math.Min(_take, 1)));
                if (list.Count == 0)
                    throw new InvalidOperationException("Sequence contains no elements");
                return list[0];
            }
        }

        public T? FirstOrDefault()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                var list = Materialize(ExecuteIdsUnlocked(Math.Min(_take, 1)));
                return list.Count == 0 ? default : list[0];
            }
        }

        public int Count()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return ExecuteIdsUnlocked(_take).Count;
            }
        }

        public bool Exists()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return ExecuteIdsUnlocked(Math.Min(_take, 1)).Count > 0;
            }
        }

        public int Delete()
        {
            _owner.Check();
            {
                _session.Check();
                try
                {
                    using var lk = _session.StartWrite();
                    var ids = ExecuteIdsUnlocked(_take);
                    var tablePage = TableRepoManager.GetTablePage(_session.Db, _table);
                    if (tablePage == null)
                        return 0;
                    var n = 0;
                    foreach (var id in ids)
                    {
                        if (TableManager.Delete(_session.Db, tablePage, id).IsSuccess)
                            n++;
                    }

                    return n;
                }
                catch
                {
                    _session.Discard();
                    throw;
                }
            }
        }

        internal OuterSnap Snapshot() => new()
        {
            Wheres = [.. _wheres],
            Extra = [.. _extra],
            OrderBy = _orderBy,
            OrderDescending = _orderDescending,
            Skip = _skip,
            Take = _take,
            Backward = _backward,
        };

        private List<T> Materialize(List<uint> ids)
        {
            var list = new List<T>(ids.Count);
            foreach (var id in ids)
            {
                var entity = _fromId(id);
                if (entity != null)
                    list.Add(entity);
            }

            return list;
        }

        private List<uint> ExecuteIdsUnlocked(int take)
        {
            var rows = RelOpExecutor.Execute(_session.Db, BuildRelOp(take));
            var ids = new List<uint>(rows.Count);
            foreach (var row in rows)
            {
                if (row.Length > 0)
                    ids.Add(row[0]);
            }

            return ids;
        }

        private RelOp BuildRelOp(int take)
        {
            var db = _session.Db;
            var outerPage = TableRepoManager.GetTablePage(db, _table);
            if (outerPage == null)
                return new ScanOp { Table = _table, Filter = new ConstExpr(false) };

            var outerMap = EntityColumnMap.FromHeaders(outerPage.ColumnHeaders);
            var outerWhere = TranslateWhere(outerMap);

            foreach (var exists in _exists)
            {
                var innerPage = TableRepoManager.GetTablePage(db, exists.Table);
                if (innerPage == null)
                    return new ScanOp { Table = _table, Filter = new ConstExpr(false) };
                var innerMap = EntityColumnMap.FromHeaders(innerPage.ColumnHeaders);
                var on = LinqToRowExpr.Translate(exists.On, [outerMap, innerMap]);
                var split = PredSplit.Split(on, innerSide: 1);
                outerWhere = RowExpr.And(outerWhere, split.OuterOnly);
            }

            RelOp op = new ScanOp
            {
                Table = _table,
                Filter = outerWhere,
                Backward = _backward && _orderBy == null,
            };

            foreach (var exists in _exists)
            {
                var innerPage = TableRepoManager.GetTablePage(db, exists.Table)!;
                var innerMap = EntityColumnMap.FromHeaders(innerPage.ColumnHeaders);
                var on = LinqToRowExpr.Translate(exists.On, [outerMap, innerMap]);
                var split = PredSplit.Split(on, innerSide: 1);
                op = new JoinOp
                {
                    Left = op,
                    Right = new ScanOp { Table = exists.Table, Filter = split.InnerOnly },
                    Equi = split.Equi,
                    Residual = split.Residual,
                    Kind = JoinKind.Semi,
                    InnerSide = 1,
                };
            }

            return new WindowOp
            {
                Input = op,
                Skip = (uint)_skip,
                Limit = take == int.MaxValue ? uint.MaxValue : (uint)take,
                OrderBy = _orderBy == null ? null : LinqToRowExpr.Translate(_orderBy, outerMap),
                OrderDescending = _orderDescending,
                OrderTables = [_table],
            };
        }

        private RowExpr? TranslateWhere(EntityColumnMap map)
        {
            RowExpr? acc = null;
            foreach (var w in _wheres)
                acc = RowExpr.And(acc, LinqToRowExpr.Translate(w, map));
            foreach (var e in _extra)
                acc = RowExpr.And(acc, e);
            return acc;
        }
    }

    internal sealed class OuterSnap
    {
        public List<LambdaExpression> Wheres { get; init; } = [];
        public List<RowExpr> Extra { get; init; } = [];
        public LambdaExpression? OrderBy { get; init; }
        public bool OrderDescending { get; init; }
        public int Skip { get; init; }
        public int Take { get; init; } = int.MaxValue;
        public bool Backward { get; init; }
    }
}
