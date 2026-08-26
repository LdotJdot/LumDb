using System.Runtime.CompilerServices;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Extension.DbEntity
{
    /// <summary>
    /// Optional hand-written entity mapping (prefer <c>[LumEntity]</c> source generation).
    /// Must implement typed WriteTo / TryReadFrom — no object[] boxing path.
    /// </summary>
    public interface IDbEntity
    {
        void WriteTo(ref RowWriter writer);

        bool TryReadFrom(IDbRow row);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDbEntity UnboxingWithId(uint id, IDbRow row)
        {
            if (!TryReadFrom(row))
                throw LumDbEngine.Element.Exceptions.LumException.Raise(LumDbEngine.Element.Exceptions.LumExceptionMessage.FailedToReadEntity);
            GetId(id);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetId(uint id)
        {
        }
    }
}
