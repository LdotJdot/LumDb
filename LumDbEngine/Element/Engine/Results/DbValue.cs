using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;

#nullable disable

namespace LumDbEngine.Element.Engine.Results
{
    internal class DbValue : DbResult, IDbValue
    {
        private object[] _objects;

        public object[] Value => _objects ??= Row?.ToObjectArray();

        public IDbRow Row { get; }

        public DbValue(object[] value)
        {
            _objects = value.ToArray();
            var cells = new DbCell[_objects.Length];
            for (int i = 0; i < _objects.Length; i++)
                cells[i] = DbCell.FromObject(_objects[i]);
            Row = new DbRow(cells);
        }

        public DbValue(DbCell[] cells)
        {
            Row = new DbRow(cells);
        }

        public DbValue(IDbRow row)
        {
            Row = row;
        }

        public DbValue()
        {
            _objects = null;
            Row = null;
        }

        public DbValue(DbResult dbResult) : base(dbResult.Exception)
        {
            Row = null;
        }

        public DbValue(LumException ex) : base(ex)
        {
            Row = null;
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
