using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Results
{
    public interface IDbValue : IDbResult
    {
        [Obsolete("Use Row typed accessors to avoid boxing.")]
        public object[] Value { get; }

        public IDbRow Row { get; }
    }

    public interface IDbValue<T> : IDbResult
    {
        public T Value { get; }
    }
}
