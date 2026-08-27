using LumDbEngine.Element.Exceptions;

namespace LumDbEngine.Element.Query
{
    /// <summary>
    /// Thread affinity: the thread that constructed the queryable is the only consumer.
    /// </summary>
    internal readonly struct QueryOwner
    {
        private readonly int _threadId;

        public QueryOwner() => _threadId = Environment.CurrentManagedThreadId;

        public void Check()
        {
            if (Environment.CurrentManagedThreadId != _threadId)
                throw LumException.Raise(LumExceptionMessage.QueryWrongThread);
        }
    }
}
