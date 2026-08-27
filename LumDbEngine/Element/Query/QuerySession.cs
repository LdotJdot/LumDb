using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Manager;

namespace LumDbEngine.Element.Query
{
    internal sealed class QuerySession
    {
        public required Action Check { get; init; }
        public required Func<IDisposable> StartRead { get; init; }
        public required Func<IDisposable> StartWrite { get; init; }
        public required Action Discard { get; init; }
        public required DbCache Db { get; init; }
        public required IDbManager Manager { get; init; }
    }
}
