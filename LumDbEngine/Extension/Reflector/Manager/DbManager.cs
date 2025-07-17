using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Utils.ByteUtils;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

// #if !NATIVE_AOT


namespace LumDbEngine.Element.Manager
{
    internal partial class DbManager : IDbManager
    {      

        public IDbValues<T> Find
                    <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : class, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues<T>(DbResults.TableNotFound);
            }

            return TableManager.TraversalType<T>(db, tablePage, condition,isBackward);
        }

      
        public IDbValue<T> Find
                    <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, string keyName, object keyValue) where T : class, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<T>(DbResults.TableNotFound);
            }
            return TableManager.PickType<T>(db, tablePage, keyName, keyValue);
        }

        public IDbValue<T> FindById<T>(DbCache db, string tableName, uint id) where T : class, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<T>(DbResults.TableNotFound);
            }
            return TableManager.PickType<T>(db, tablePage, id);
        }

        public IDbResult Update
                    <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, T value, Func<T, bool> condition) where T : class, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNodeType<T>(db, tablePage, condition);
            if (dataNode == null)
            {
                return DbResults.ConditionNotMatched;
            }

            TableManager.Update(db, tablePage, dataNode, ReflectorUtils.Load(value));

            return DbResults.Success;
        }

        public IDbResult Update<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, uint id, T value) where T : class, new()
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

            TableManager.Update(db, tablePage, dataNode, ReflectorUtils.Load(value));

            return DbResults.Success;
        }

        public IDbResult Update
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, string tableName, string keyName, object keyValue, T value) where T : class, new()
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

            TableManager.Update(db, tablePage, dataNode, ReflectorUtils.Load(value));

            return DbResults.Success;
        }


        public IDbValues<T> Find
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, string tableName, Func<T, bool> condition, bool isBackward, uint skip, uint limit) where T : class, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues<T>(DbResults.TableNotFound);
            }
            return TableManager.Find<T>(db, tablePage, condition, isBackward, skip, limit);
        }

        public void GoThrough
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, string tableName, Func<T, bool> action) where T : class, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage != null)
            {
                TableManager.GoThroughType<T>(db, tablePage, action);
            }

        }       

    }
}


// #endif