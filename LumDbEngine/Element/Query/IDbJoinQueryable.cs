using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine
{
    /// <summary>
    /// Two-table inner join. Terminal methods run the whole plan under one lock.
    /// Chain <see cref="Join{T3}"/> for a third table.
    /// </summary>
    public interface IDbJoinQueryable<T1, T2>
    {
        IDbJoinQueryable<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate);

        IDbJoinQueryable<T1, T2> OrderBy<TKey>(Expression<Func<T1, T2, TKey>> keySelector);

        IDbJoinQueryable<T1, T2> OrderByDescending<TKey>(Expression<Func<T1, T2, TKey>> keySelector);

        IDbJoinQueryable<T1, T2> Skip(int count);

        IDbJoinQueryable<T1, T2> Take(int count);

        IDbJoin3Queryable<T1, T2, T3> Join<T3>(string innerTable, Expression<Func<T1, T2, T3, bool>> on)
            where T3 : ILumEntity<T3>, new();

        IDbJoin3Queryable<T1, T2, T3> JoinEntity<T3>(string innerTable, Expression<Func<T1, T2, T3, bool>> on)
            where T3 : IDbEntity, new();

        IDbValues<(T1 Outer, T2 Inner)> ToValues();

        List<(T1 Outer, T2 Inner)> ToList();

        (T1 Outer, T2 Inner)[] ToArray();

        (T1 Outer, T2 Inner) First();

        (T1 Outer, T2 Inner)? FirstOrDefault();

        int Count();

        bool Exists();
    }

    /// <summary>
    /// Three-table inner join. Terminal methods run the whole plan under one lock.
    /// </summary>
    public interface IDbJoin3Queryable<T1, T2, T3>
    {
        IDbJoin3Queryable<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate);

        IDbJoin3Queryable<T1, T2, T3> OrderBy<TKey>(Expression<Func<T1, T2, T3, TKey>> keySelector);

        IDbJoin3Queryable<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, T2, T3, TKey>> keySelector);

        IDbJoin3Queryable<T1, T2, T3> Skip(int count);

        IDbJoin3Queryable<T1, T2, T3> Take(int count);

        IDbValues<(T1 A, T2 B, T3 C)> ToValues();

        List<(T1 A, T2 B, T3 C)> ToList();

        (T1 A, T2 B, T3 C)[] ToArray();

        (T1 A, T2 B, T3 C) First();

        (T1 A, T2 B, T3 C)? FirstOrDefault();

        int Count();

        bool Exists();
    }
}
