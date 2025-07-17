using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.StringUtils;
using System.Text;

namespace LumDbEngine.Element.Manager.Specific
{
    internal  static partial class TableManager
    {
     
        public static IDbValues<Entity> Traversal<Entity>(DbCache db, TablePage tablePage, Func<IEnumerable<Entity>, IEnumerable<Entity>> condition, bool isBackward) where Entity : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValues<Entity>([]);
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values =isBackward? DataManager.GetValues_Backward(db, tablePage.ColumnHeaders, rootPage!) : DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!);
            return new DbValues<Entity>(condition(values.Select(o => (Entity)(new Entity()).UnboxingWithId(o.node.Id, o.data))));
        }

        public static DataNode? FirstOrDefaultNode<Entity>(DbCache db, TablePage tablePage, Func<Entity, bool> condition) where Entity : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return null;
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values = DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!);
            var t = new Entity();

            foreach (var tarGet in values)
            {
                t.Unboxing(tarGet.data);
                if (condition(t))
                {
                    return tarGet.node;
                }
            }
            return null;
        }

        public static IDbValue<Entity> Pick<Entity>(DbCache db, TablePage tablePage, uint id) where Entity : IDbEntity, new()
        {
            var node = FirstOrDefaultNode(db, tablePage, id);

            if (node == null)
            {
                return new DbValue<Entity>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));
            }
            else
            {
                return new DbValue<Entity>((Entity)new Entity().UnboxingWithId(node.Id, DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data)));
            }
        }

        public static IDbValue<Entity> Pick<Entity>(DbCache db, TablePage tablePage, string keyName, object keyValue) where Entity : IDbEntity, new()
        {
            var headerIndex = tablePage.GetTableHeaderIndex(keyName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];

            if (columnHeader.IsKey == false)
            {
                return new DbValue<Entity>(LumException.Raise($"{keyName} {LumExceptionMessage.NotKey}"));
            }

            if (!columnHeader.ValueType.CheckType(keyValue) || !columnHeader.ValueType.IsValidFix32())
            {
                return new DbValue<Entity>(LumException.Raise($"{LumExceptionMessage.DataTypeNotSupport}: {columnHeader.ValueType}"));
            }

            var len = columnHeader.ValueType.GetLength();
            Span<byte> buffer = stackalloc byte[len];
            keyValue.SerializeObjectToBytes(buffer);
            var node = IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex).Value, buffer);

            if (node == null)
            {
                return new DbValue<Entity>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, {keyName}: {keyValue}"));
            }
            else
            {
                return new DbValue<Entity>((Entity)new Entity().UnboxingWithId(node.Id, DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data)));
            }
        }

        
        public static unsafe IDbValues<Entity> Find<Entity>(DbCache db, TablePage tablePage, (string keyName, Func<object, bool> checkFunc)[]? conditions,bool isBackforward,uint skip,uint limit) where Entity : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValues<Entity>([]);
            }

            Func<object, bool>?[]? fullCondition = null;

            if (conditions?.Length > 0)
            {
                fullCondition = new Func<object, bool>?[tablePage.ColumnCount];
                
                var headerStringNames = tablePage.ColumnHeaders.Select(o => Encoding.UTF8.GetString(o.Name).TrimEnd('\0')).ToArray();
                var keyStringNames = conditions.Select(o => o.keyName).ToArray();

                // not done

                for (int i = 0; i < fullCondition.Length; i++)
                {
                    for (int j = 0; j < conditions.Length; j++)
                    {
                        if (keyStringNames[j].Equals(headerStringNames[i], StringComparison.Ordinal))
                        {
                            fullCondition[i] = conditions[j].checkFunc;
                            keyStringNames[j] = string.Empty;
                            break;
                        }
                    }
                }
            }
            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values = isBackforward ? DataManager.GetValuesWithIdCondition_Backward(db, tablePage.ColumnHeaders, rootPage!, fullCondition, skip, limit):  DataManager.GetValuesWithIdCondition(db, tablePage.ColumnHeaders,rootPage!, fullCondition, skip, limit);
            return new DbValues<Entity>(values.Select(o => (Entity)(new Entity()).UnboxingWithId(o.node.Id, o.data)));
        }

        public static void GoThrough<Entity>(DbCache db, TablePage tablePage, Func<Entity, bool> action) where Entity : IDbEntity, new()
        {
            if (db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
                DataManager.GoThrough_Entity(db, tablePage.ColumnHeaders, rootPage!,action);
            }

        }        
    }
}