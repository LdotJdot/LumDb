using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.ByteUtils;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Manager.Specific
{
    internal partial class DataManager
    {   

        internal static void GoThrough_Entity<Entity>(DbCache db, ColumnHeader[] headers, DataPage? page, Func<Entity, bool> action) where Entity : IDbEntity, new()
        {
            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable)
                    {
                        var newEntity= (new Entity()).UnboxingWithId(dataNode.Id, GetValue(db, headers, dataNode.Data));

                        if (!action((Entity)newEntity))
                        {
                            return;
                        }
                    }
                }

                if (db.IsValidPage(page.NextPageId))
                {
                    page = PageManager.GetPage<DataPage>(db, page.NextPageId);
                }
                else
                {
                    page = null;
                }
            }
        }     
    }
}