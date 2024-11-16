using LumDbEngine.Element.Exceptions;

#nullable disable

namespace LumDbEngine.Element.Engine.Results
{
    internal static class DbResults
    {
        public static readonly DbResult Success = GetSuccess();
        public static readonly DbResult OneColumnAtLeast = GetFail("One column at least");
        public static readonly DbResult ExcessiveNumberOfColumns = GetFail("Excessive number of columns");
        public static readonly DbResult TableNotFound = GetFail("Table not found or null.");
        public static readonly DbResult DataNotFound = GetFail("Data not found.");
        public static readonly DbResult DataNumberNotMatchTableColumns = GetFail("Data number not match table columns.");
        public static readonly DbResult ConditionNotMatched = GetFail("Condition not matched.");
        public static readonly DbResult TableAlreadyExisted = GetFail("Table already existed.");

        internal static DbResult GetSuccess()
        {
            return new DbResult();
        }

        internal static DbResult GetFail(string msg)
        {
            return new DbResult(LumException.Raise(msg));
        }
    }
}