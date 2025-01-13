using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;

#nullable disable

namespace LumDbEngine.Element.Engine.Results
{
    internal class DbValues<T> : DbResult, IDbValues<T>
    {
        public IReadOnlyList<T> Values { get; } = null;

        public DbValues(IEnumerable<T> values)
        {
            Values = values.ToArray();
        }

        public DbValues()
        {
            Values = null;
        }

        public DbValues(DbResult res) : base(res.Exception)
        {
        }

        public DbValues(LumException ex) : base(ex)
        {
        }
    }

    internal class DbValues : DbResult, IDbValues
    {
        public IReadOnlyList<object[]> Values { get; } = null;

        public int ColumnCount { get; set; } = -1;

        public DbValues(int count, IEnumerable<object[]> values)
        {
            ColumnCount = count;
            Values = values.ToArray();
        }

        public DbValues()
        {
        }

        public DbValues(LumException ex) : base(ex)
        {
        }

        public DbValues(DbResult res) : base(res.Exception)
        {
        }
              
    }
}