using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Value
{
    /// <summary>
    /// Interface to wrapper class object for Db engine
    /// </summary>
    public interface IDbEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDbEntity UnboxingWithId(uint id, object[] obj)
        {
            Unboxing(obj);
            GetId(id);
            return this;
        }

        /// <summary>
        /// Unboxing the values
        /// </summary>
        /// <param name="obj"> The object array store in table with preset order</param>
        /// <returns></returns>
        public IDbEntity Unboxing(object[] obj);

        /// <summary>
        /// Boxing the values
        /// </summary>
        /// <returns> The object array with same order of that store in table</returns>
        public object[] Boxing();

        /// <summary>
        /// Get the default id of data.
        /// </summary>
        /// <param name="id">the internal auto-incrementing id of each data</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetId(uint id)
        {
        }
    }
}