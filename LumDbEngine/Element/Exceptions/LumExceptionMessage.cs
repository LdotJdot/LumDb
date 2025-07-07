using System.Data.Common;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Exceptions
{
    internal class LumExceptionMessage
    {
        internal const string IllegaTransaction = "In a single-threaded environment, you attempted to use an read only transaction immediately after a normal transaction.";
        internal const string TransactionTimeout = "Transaction time out.";
        internal const string DateTimeUtcError = "DateTime should be Utc type.";
        internal const string UnknownValType = "Unknown value type";
        internal const string DataTypeNotSupport = "The value type is not supported, or check the length.";
        internal const string ColumnElementNotEqual = "The number of inputs is not equal with that of column element";
        internal const string ColumnNameNotExisted = "Column name is not existed";
        internal const string KeyNoFound = "Key not found,";
        internal const string DataNoFound = "Data not found,";
        internal const string DuplicateColumnHeader = "Duplicate column headers found";
        internal const string NotKey = "is not key";
        internal const string DbEngDisposedTimeOut = "Waiting living transactions  timeout when disposing DbEngine.";
        internal const string DbEngDisposedEarly = "Transaction cannot be accessed beacuse the dbEngine has already be disposed early.";
        internal const string InternalError = "InternalError";
    }
}