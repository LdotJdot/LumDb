using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics.CodeAnalysis;
using System.Text;

// #if !NATIVE_AOT

namespace LumDbEngine.Element.Manager.Specific
{
    internal static partial class TableManager
    {     

        public static IDbValues<T> TraversalType
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (DbCache db, TablePage tablePage, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : class, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValues<T>([]);
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values =isBackward? DataManager.GetValues_Backward(db, tablePage.ColumnHeaders, rootPage!) : DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!);
            
            return new DbValues<T>(condition(values.Select(o =>
            ReflectorUtils.Dump(new T(), o.node.Id, o.data))));
        }

        public static DataNode? FirstOrDefaultNodeType
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (DbCache db, TablePage tablePage, Func<T, bool> condition) where T : class, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return null;
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values = DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!);

            var t = new T();

            foreach (var tarGet in values)
            {
                ReflectorUtils.Dump(t, tarGet.node.Id, tarGet.data);
                if (condition(t))
                {
                    return tarGet.node;
                }
            }
            return null;
        }      

        public static IDbValue<T> PickType
          <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (DbCache db, TablePage tablePage, uint id) where T : class, new()
        {
            var node = FirstOrDefaultNode(db, tablePage, id);

            if (node == null)
            {
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));
            }
            else
            {
                return new DbValue<T>(ReflectorUtils.Dump(new T(), node.Id, DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data)));
            }
        }

        public static IDbValue<T> PickType
         <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (DbCache db, TablePage tablePage, string keyName, object keyValue) where T : class, new()
        {
            var headerIndex = tablePage.GetTableHeaderIndex(keyName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];

            if (columnHeader.IsKey == false)
            {
                return new DbValue<T>(LumException.Raise($"{keyName} {LumExceptionMessage.NotKey}"));
            }

            if (!columnHeader.ValueType.CheckType(keyValue) || !columnHeader.ValueType.IsValidFix32())
            {
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.DataTypeNotSupport}: {columnHeader.ValueType}"));
            }

            var len = columnHeader.ValueType.GetLength();
            Span<byte> buffer = stackalloc byte[len];
            keyValue.SerializeObjectToBytes(buffer);
            var node = IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex).Value, buffer);

            if (node == null)
            {
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, {keyName}: {keyValue}"));
            }
            else
            {
                return new DbValue<T>(ReflectorUtils.Dump(new T(),node.Id, DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data)));
            }
        }

        public static unsafe IDbValues<T> Find
         <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (DbCache db, TablePage tablePage, Func<T, bool> condition, bool isBackforward,uint skip,uint limit) where T : class, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValues<T>([]);
            }
           
            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values = isBackforward ? 
                DataManager.GetValuesWithIdCondition_Backward(db, tablePage.ColumnHeaders, rootPage!, condition, skip, limit):
                DataManager.GetValuesWithIdCondition(db, tablePage.ColumnHeaders,rootPage!, condition, skip, limit);
            return new DbValues<T>(values);
        }

        public static void GoThroughType
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (DbCache db, TablePage tablePage, Func<T, bool> action) where T : class, new()
        {
            if (db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
                DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage!,action);
            }

        }        
    }
}
// #endif