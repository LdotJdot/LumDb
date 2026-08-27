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
        /// <summary>
        /// Pull iterator over live rows. Page walk is the same as <see cref="GoThrough_Entity"/>
        /// (Root → Next; backward walks Next to the chain end then Prev). LINQ Where/Skip/Take
        /// on this sequence stops the pull — no full-table List.
        /// </summary>
        internal static IEnumerable<Entity> EnumerateEntities<Entity>(DbCache db, ColumnHeader[] headers, DataPage? page, bool backward)
            where Entity : IDbEntity, new()
        {
            if (page == null)
                yield break;

            if (backward)
            {
                while (db.IsValidPage(page.NextPageId))
                    page = PageManager.GetPage<DataPage>(db, page.NextPageId);

                while (page != null)
                {
                    for (int i = page.MaxDataCount - 1; i >= 0; i--)
                    {
                        var entity = TryMaterialize<Entity>(db, headers, page.DataNodes[i]);
                        if (entity != null)
                            yield return entity;
                    }

                    page = db.IsValidPage(page.PrevPageId)
                        ? PageManager.GetPage<DataPage>(db, page.PrevPageId)
                        : null;
                }
            }
            else
            {
                while (page != null)
                {
                    for (int i = 0; i < page.MaxDataCount; i++)
                    {
                        var entity = TryMaterialize<Entity>(db, headers, page.DataNodes[i]);
                        if (entity != null)
                            yield return entity;
                    }

                    page = db.IsValidPage(page.NextPageId)
                        ? PageManager.GetPage<DataPage>(db, page.NextPageId)
                        : null;
                }
            }
        }

        internal static void GoThrough_Entity<Entity>(DbCache db, ColumnHeader[] headers, DataPage? page, Func<Entity, bool> action)
            where Entity : IDbEntity, new()
        {
            foreach (var entity in EnumerateEntities<Entity>(db, headers, page, backward: false))
            {
                if (!action(entity))
                    return;
            }
        }

        private static Entity? TryMaterialize<Entity>(DbCache db, ColumnHeader[] headers, DataNode dataNode)
            where Entity : IDbEntity, new()
        {
            if (dataNode == null || !dataNode.IsAvailable)
                return default;

            var row = CreateRowBuffer(db, headers, dataNode.Data);
            var entity = new Entity();
            entity.UnboxingWithId(dataNode.Id, row);
            return entity;
        }
    }
}
