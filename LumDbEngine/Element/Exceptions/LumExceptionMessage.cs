using System.Data.Common;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Exceptions
{
    internal class LumExceptionMessage
    {
        internal const string SingleThreadMultiTransaction = "In a single thread, the previous transaction should be disposed before starting another one.";
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
        internal const string DbEngDisposedTimeOut = "Transactions waiting timeout when tried to dispose DbEngine.";
        internal const string DbEngDisposedEarly = "Transaction cannot be disposed beacuse the dbEngine has already be disposed early.";
    }
}