using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Manager.Specific
{
    internal partial class DataManager
    {
        internal static void GoThrough_Entity<Entity>(DbCache db, ColumnHeader[] headers, DataPage? page, Func<Entity, bool> action)
            where Entity : IDbEntity, new()
        {
            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];
                    if (!dataNode.IsAvailable)
                        continue;

                    var row = CreateRowBuffer(db, headers, dataNode.Data);
                    var entity = new Entity();
                    entity.UnboxingWithId(dataNode.Id, row);
                    if (!action(entity))
                        return;
                }

                page = db.IsValidPage(page.NextPageId)
                    ? PageManager.GetPage<DataPage>(db, page.NextPageId)
                    : null;
            }
        }
    }
}
