using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;

#nullable disable

namespace LumDbEngine.Element.Engine.Results
{
    internal class DbValue : DbResult, IDbValue
    {
        public object[] Value { get; } = null;

        public DbValue(object[] value)
        {
            Value = value.ToArray();
        }

        public DbValue()
        {
            Value = null;
        }

        public DbValue(DbResult dbResult) : base(dbResult.Exception)
        {
        }

        public DbValue(LumException ex) : base(ex)
        {
        }       
    }

    internal class DbValue<T> : DbResult, IDbValue<T>
    {
        public T Value { get; } = default;

        public DbValue(T value)
        {
            Value = value;
        }

        public DbValue()
        {
            Value = default;
        }

        public DbValue(DbResult dbResult) : base(dbResult.Exception)
        {
        }

        public DbValue(LumException ex) : base(ex)
        {
        }
    }
}