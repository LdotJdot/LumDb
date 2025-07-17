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

        public IDbValue<uint> Insert
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, T t) where T : class
        {
            (string columnName, object value)[] values = [];

            try
            {
                values = ReflectorUtils.GetValues(t);
            }
            catch (Exception ex)
            {

            }

            return Insert(tableName,values);
        }

      
    }
}

// #endif