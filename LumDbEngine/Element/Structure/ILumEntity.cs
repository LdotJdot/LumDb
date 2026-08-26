using LumDbEngine.Element.Structure;

namespace LumDbEngine
{
    /// <summary>
    /// Static abstract contract implemented by source-generated partial entity types.
    /// </summary>
    public interface ILumEntity<T> where T : ILumEntity<T>, new()
    {
        static abstract (string columnName, DbValueType type, bool isKey)[] DbSchema { get; }

        static abstract void WriteTo(ref RowWriter writer, in T value);

        static abstract bool TryReadFrom(IDbRow row, out T value);

        /// <summary>Optional: assign engine auto id when entity has [Id] uint property.</summary>
        static abstract void SetDbId(ref T value, uint id);
    }

    /// <summary>Self-registration hook (cctor of generated types).</summary>
    public static class LumEntityProvider
    {
        public static void Register<T>() where T : ILumEntity<T>, new()
        {
            // Generic constraint path needs no runtime dictionary; call is for side-effect / AOT rooting.
            _ = T.DbSchema;
        }
    }
}
