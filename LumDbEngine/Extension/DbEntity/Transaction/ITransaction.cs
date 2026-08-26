using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    public partial interface ITransaction : IDisposable
    {
        IDbValue<uint> Insert_Entity<Entity>(string tableName, Entity t) where Entity : IDbEntity, new();

        IDbValues<Entity> Find_Entity<Entity>(string tableName, Func<IEnumerable<Entity>, IEnumerable<Entity>> condition, bool isBackward = false)
            where Entity : IDbEntity, new();

        IDbValue<Entity> Find_Entity<Entity>(string tableName, string keyName, DbCell keyValue) where Entity : IDbEntity, new();

        IDbValue<Entity> Find_Entity<Entity>(string tableName, uint id) where Entity : IDbEntity, new();

        IDbResult Update_Entity<Entity>(string tableName, string keyName, DbCell keyValue, Entity value) where Entity : IDbEntity, new();

        IDbResult Update_Entity<Entity>(string tableName, uint id, Entity value) where Entity : IDbEntity, new();

        IDbResult Update_Entity<Entity>(string tableName, Func<Entity, bool> condition, Entity value) where Entity : IDbEntity, new();

        void GoThrough_Entity<Entity>(string tableName, Func<Entity, bool> action) where Entity : IDbEntity, new();
    }
}
