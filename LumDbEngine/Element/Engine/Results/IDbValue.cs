using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Results
{
    public interface IDbValue : IDbResult
    {
        public object[] Value { get; }
    }

    public interface IDbValue<T> : IDbResult
    {
        public T Value { get; }
    }
}