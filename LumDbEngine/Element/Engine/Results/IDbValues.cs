using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Results
{
    public interface IDbValues : IDbResult
    {
        public int ColumnCount { get; }

        public DbValueType[] GetValueTypes();

        public DbValueType GetValueType(string index);

        public IReadOnlyList<object[]> Values { get; }
    }

    public interface IDbValues<T> : IDbResult
    {
        public IReadOnlyList<T> Values { get; }
    }
}