using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;

#nullable disable

namespace LumDbEngine.Element.Engine.Results
{
    internal class DbValue : DbResult, IDbValue
    {
        private Dictionary<string, DbValueType> types = null;
        public object[] Value { get; } = null;

        public DbValue(Dictionary<string, DbValueType> types, object[] value)
        {
            this.types = types;
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

        public DbValueType[] GetValueTypes()
        {
            return types?.Values.ToArray() ?? [];
        }

        public DbValueType GetValueType(string index)
        {
            return types?[index] ?? DbValueType.Unknow;
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