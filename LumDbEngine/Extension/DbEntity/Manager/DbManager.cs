using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.ByteUtils;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace LumDbEngine.Element.Manager
{
    internal partial class DbManager : IDbManager
    {
        
        public IDbValue<uint> Insert_Entity<Entity>(DbCache db, string tableName, Entity t) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<uint>(DbResults.TableNotFound);
            }

            var vls = t.Boxing();

            if (vls.Length != tablePage.PageHeader.ColumnCount)
            {
                return new DbValue<uint>(DbResults.DataNumberNotMatchTableColumns);
            }

            var tbd = new TableValue[tablePage.PageHeader.ColumnCount];
            for (int i = 0; i < tablePage.PageHeader.ColumnCount; i++)
            {
                tbd[i] = (tablePage.ColumnHeaders[i].Name.TransformToToString(), vls[i]);
            }

            var id = TableManager.InsertData(db, tablePage, tbd);

            return new DbValue<uint>(id ?? 0);
        }

        public IDbValues<Entity> Find_Entity<Entity>(DbCache db, string tableName, Func<IEnumerable<Entity>, IEnumerable<Entity>> condition, bool isBackward) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues<Entity>(DbResults.TableNotFound);
            }

            return TableManager.Traversal<Entity>(db, tablePage, condition,isBackward);
        }

       
        public IDbValue<Entity> Find_Entity<Entity>(DbCache db, string tableName, string keyName, object keyValue) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<Entity>(DbResults.TableNotFound);
            }
            return TableManager.Pick<Entity>(db, tablePage, keyName, keyValue);
        }

        public IDbValue<Entity> FindById_Entity<Entity>(DbCache db, string tableName, uint id) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<Entity>(DbResults.TableNotFound);
            }
            return TableManager.Pick<Entity>(db, tablePage, id);
        }

        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, Entity value, Func<Entity, bool> condition) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode<Entity>(db, tablePage, condition);
            if (dataNode == null)
            {
                return DbResults.ConditionNotMatched;
            }

            TableManager.Update(db, tablePage, dataNode, value.Boxing());

            return DbResults.Success;
        }

        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, uint id, Entity value) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, id);

            if (dataNode == null)
            {
                return DbResults.ConditionNotMatched;
            }

            TableManager.Update(db, tablePage, dataNode, value.Boxing());

            return DbResults.Success;
        }


        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, string keyName, object keyValue, Entity value) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, keyName, keyValue);

            if (dataNode == null)
            {
                return DbResults.DataNotFound;
            }

            TableManager.Update(db, tablePage, dataNode, value.Boxing());

            return DbResults.Success;
        }



        public IDbValues<Entity> Find_Entity<Entity>(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues<Entity>(DbResults.TableNotFound);
            }
            return TableManager.Find<Entity>(db, tablePage, conditions, isBackward, skip, limit);
        }

        public void GoThrough_Entity<Entity>(DbCache db, string tableName, Func<Entity, bool> action) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage != null)
            {
                TableManager.GoThrough<Entity>(db, tablePage, action);
            }

        }
        

    }
}