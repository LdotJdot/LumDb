using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Manager
{
    internal partial interface IDbManager
    {

        public IDbValue<uint> Insert_Entity<Entity>(DbCache db, string tableName, Entity t) where Entity : IDbEntity, new();


        public IDbValues<Entity> Find_Entity<Entity>(DbCache db, string tableName, Func<IEnumerable<Entity>, IEnumerable<Entity>> condition, bool isBackward) where Entity : IDbEntity, new();


     
        public IDbValue<Entity> Find_Entity<Entity>(DbCache db, string tableName, string keyName, object keyValue) where Entity : IDbEntity, new();

        public IDbValue<Entity> FindById_Entity<Entity>(DbCache db, string tableName, uint id) where Entity : IDbEntity, new();


        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, Entity value, Func<Entity, bool> condition) where Entity : IDbEntity, new();

        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, uint id, Entity value) where Entity : IDbEntity, new();


        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, string keyName, object keyValue, Entity value) where Entity : IDbEntity, new();



        public IDbValues<Entity> Find_Entity<Entity>(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit) where Entity : IDbEntity, new();



        public void GoThrough_Entity<Entity>(DbCache db, string tableName, Func<Entity, bool> action) where Entity : IDbEntity, new();



    }
}