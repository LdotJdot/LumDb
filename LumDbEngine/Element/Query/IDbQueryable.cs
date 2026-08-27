using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Element.Structure;

namespace LumDbEngine
{
    /// <summary>
    /// Fluent query. Where / Join / Skip only build a plan (no I/O). Terminals run under one transaction lock.
    /// Thread affinity: the thread that called <c>Query()</c> must consume this instance. Other threads
    /// get an exception and should call <c>Query()</c> themselves.
    /// </summary>
    public interface IDbQueryable<T>
    {
        IDbQueryable<T> Where(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Callback on the materialized entity. Allocates T for every scanned row (skipped rows too).
        /// Use for Regex / custom logic. Prefer expression Where or <see cref="Where(RowViewPredicate)"/> when possible.
        /// </summary>
        IDbQueryable<T> WhereCallback(Func<T, bool> predicate);

        /// <summary>
        /// Zero-alloc callback on <see cref="RowView"/> (column ordinals). AOT-safe.
        /// </summary>
        IDbQueryable<T> Where(RowViewPredicate predicate);

        IDbQueryable<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);

        IDbQueryable<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);

        IDbQueryable<T> Skip(int count);

        IDbQueryable<T> Take(int count);

        /// <summary>
        /// Occupancy-order reverse (page Prev chain). Ignored when <see cref="OrderBy{TKey}"/> is set.
        /// </summary>
        IDbQueryable<T> Reverse();

        IDbJoinQueryable<T, TInner> Join<TInner>(string innerTable, Expression<Func<T, TInner, bool>> on)
            where TInner : ILumEntity<TInner>, new();

        IDbJoinQueryable<T, TInner> JoinEntity<TInner>(string innerTable, Expression<Func<T, TInner, bool>> on)
            where TInner : IDbEntity, new();

        /// <summary>
        /// SQL EXISTS: keep outer rows that have at least one inner match. Inner rows are not materialized.
        /// </summary>
        IDbQueryable<T> WhereExists<TInner>(string innerTable, Expression<Func<T, TInner, bool>> on);

        IDbValues<T> ToValues();

        List<T> ToList();

        T[] ToArray();

        T First();

        T? FirstOrDefault();

        int Count();

        bool Exists();

        int Delete();
    }
}
