#nullable disable

namespace LumDbEngine.Element.Engine.Results
{
    internal class DbResult : IDbResult
    {
        public bool IsSuccess => Exception == null;
        public Exception Exception { get; } = null;

        internal DbResult()
        {
        }

        internal DbResult(Exception exception)
        {
            Exception = exception;
        }
    }
}