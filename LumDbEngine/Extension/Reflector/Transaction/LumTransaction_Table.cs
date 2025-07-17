using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using System.Diagnostics.CodeAnalysis;

// #if !NATIVE_AOT
namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult Create
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName) where T : class
        {
            var tableHeader = ReflectorUtils.GetPropertity<T>();
            return Create(tableName, tableHeader);
        }
    }
}

// #endif