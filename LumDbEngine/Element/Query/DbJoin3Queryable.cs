using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Query;

namespace LumDbEngine
{
    internal sealed class DbJoin3Queryable<T1, T2, T3> : IDbJoin3Queryable<T1, T2, T3>
    {
        private readonly QueryOwner _owner = new();
        private readonly QuerySession _session;
        private readonly string _t0, _t1, _t2;
        private readonly OuterSnap _outer;
        private readonly LambdaExpression _on1;
        private readonly List<LambdaExpression> _wheres2;
        private readonly int _skip2, _take2;
        private readonly LambdaExpression? _order2;
        private readonly bool _desc2;
        private readonly LambdaExpression _on2;
        private readonly Func<uint, T1?> _from0;
        private readonly Func<uint, T2?> _from1;
        private readonly Func<uint, T3?> _from2;
        private readonly List<LambdaExpression> _wheres3 = new();
        private LambdaExpression? _order3;
        private bool _desc3;
        private int _skip3;
        private int _take3 = int.MaxValue;

        internal DbJoin3Queryable(
            QuerySession session,
            string t0, string t1, string t2,
            OuterSnap outer,
            LambdaExpression on1,
            List<LambdaExpression> wheres2,
            int skip2, int take2,
            LambdaExpression? order2, bool desc2,
            LambdaExpression on2,
            Func<uint, T1?> from0,
            Func<uint, T2?> from1,
            Func<uint, T3?> from2)
        {
            _session = session;
            _t0 = t0;
            _t1 = t1;
            _t2 = t2;
            _outer = outer;
            _on1 = on1;
            _wheres2 = wheres2;
            _skip2 = skip2;
            _take2 = take2;
            _order2 = order2;
            _desc2 = desc2;
            _on2 = on2;
            _from0 = from0;
            _from1 = from1;
            _from2 = from2;
        }

        public IDbJoin3Queryable<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _owner.Check();
            {
                _wheres3.Add(predicate);
                return this;
            }
        }

        public IDbJoin3Queryable<T1, T2, T3> OrderBy<TKey>(Expression<Func<T1, T2, T3, TKey>> keySelector)
        {
            ArgumentNullException.ThrowIfNull(keySelector);
            _owner.Check();
            {
                if (_order3 != null)
                    throw LumException.Raise(LumExceptionMessage.OrderByAlreadyDefined);
                _order3 = keySelector;
                _desc3 = false;
                return this;
            }
        }

        public IDbJoin3Queryable<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, T2, T3, TKey>> keySelector)
        {
            ArgumentNullException.ThrowIfNull(keySelector);
            _owner.Check();
            {
                if (_order3 != null)
                    throw LumException.Raise(LumExceptionMessage.OrderByAlreadyDefined);
                _order3 = keySelector;
                _desc3 = true;
                return this;
            }
        }

        public IDbJoin3Queryable<T1, T2, T3> Skip(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            _owner.Check();
            {
                _skip3 = count;
                return this;
            }
        }

        public IDbJoin3Queryable<T1, T2, T3> Take(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            _owner.Check();
            {
                _take3 = count;
                return this;
            }
        }

        public IDbValues<(T1 A, T2 B, T3 C)> ToValues()
        {
            _owner.Check();
                return new DbValues<(T1, T2, T3)>(ExecuteTriples(_take3));
        }

        public List<(T1 A, T2 B, T3 C)> ToList()
        {
            var values = ToValues();
            if (!values.IsSuccess)
                throw values.Exception ?? LumException.Raise("query failed");
            return values.Values.ToList();
        }

        public (T1 A, T2 B, T3 C)[] ToArray() => ToList().ToArray();

        public (T1 A, T2 B, T3 C) First()
        {
            _owner.Check();
            {
                var list = ExecuteTriples(Math.Min(_take3, 1));
                if (list.Count == 0)
                    throw new InvalidOperationException("Sequence contains no elements");
                return list[0];
            }
        }

        public (T1 A, T2 B, T3 C)? FirstOrDefault()
        {
            _owner.Check();
            {
                var list = ExecuteTriples(Math.Min(_take3, 1));
                return list.Count == 0 ? null : list[0];
            }
        }

        public int Count()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return RelOpExecutor.Execute(_session.Db, BuildRelOp(_take3)).Count;
            }
        }

        public bool Exists()
        {
            _owner.Check();
            {
                _session.Check();
                using var lk = _session.StartRead();
                return RelOpExecutor.Execute(_session.Db, BuildRelOp(Math.Min(_take3, 1))).Count > 0;
            }
        }

        private List<(T1 A, T2 B, T3 C)> ExecuteTriples(int take3)
        {
            _session.Check();
            using var lk = _session.StartRead();
            var tuples = RelOpExecutor.Execute(_session.Db, BuildRelOp(take3));
            var list = new List<(T1, T2, T3)>(tuples.Count);
            foreach (var t in tuples)
            {
                if (t.Length < 3)
                    continue;
                var a = _from0(t[0]);
                var b = _from1(t[1]);
                var c = _from2(t[2]);
                if (a != null && b != null && c != null)
                    list.Add((a, b, c));
            }

            return list;
        }

        private RelOp BuildRelOp(int take3)
            => RelOpFactory.ThreeTable(
                _session.Db, _t0, _t1, _t2, _outer,
                _on1, _wheres2, _skip2, _take2, _order2, _desc2,
                _on2, _wheres3, _skip3, take3, _order3, _desc3);
    }
}
