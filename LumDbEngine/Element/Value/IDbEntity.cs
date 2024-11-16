using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Value
{
    public interface IDbEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDbEntity UnboxingWithId(uint id, object[] obj)
        {
            Unboxing(obj);
            SetId(id);
            return this;
        }

        public IDbEntity Unboxing(object[] obj);

        public object[] Boxing();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetId(uint id)
        {
        }
    }
}