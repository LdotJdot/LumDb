namespace LumDbEngine.Element.Engine.Results
{
    public interface IDbResult
    {
        public Exception? Exception { get; }

        public bool IsSuccess { get; }
    }
}