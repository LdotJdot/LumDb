using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;

namespace UnitTestLumDb.Config
{
    internal static class RowScan
    {
        public static List<object[]> ToObjectRows(ITransaction ts, string table)
        {
            var list = new List<object[]>();
            ts.GoThrough(table, (ref RowView row) =>
            {
                var arr = new object[row.ColumnCount];
                for (int i = 0; i < row.ColumnCount; i++)
                    arr[i] = row.GetCell(i).ToObject();
                list.Add(arr);
                return true;
            });
            return list;
        }

        public static int Count(ITransaction ts, string table, RowViewPredicate? pred = null)
        {
            int n = 0;
            ts.GoThrough(table, (ref RowView row) =>
            {
                if (pred == null || pred(ref row))
                    n++;
                return true;
            });
            return n;
        }
    }
}
