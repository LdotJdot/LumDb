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
        internal const string KeyAlreadyExisted = "Key already existed";
        internal const string FixedLengthTooLong = "Value exceeds fixed column length";
        internal const string FailedToReadEntity = "failed to read entity";
        internal const string ColumnTypeMismatch = "DbCell type does not match column type";
        internal const string UnsupportedLinq =
            "Expression cannot be translated to RowView. Supported: comparisons, && || !, arithmetic, string/Math helpers, ordinary C# methods (int Add(int,int)), captured Func/delegates, generic helpers, captured constants. Or inject WhereCallback / Where(RowViewPredicate). Culture overloads, Split, Action, and BCL instance methods are not supported.";
        internal const string OrderByAlreadyDefined = "ORDER BY already defined in this query";
        internal const string QueryWrongThread =
            "This query object can only be used by the thread that called Query(). Other threads must call Query() themselves.";
    }
}