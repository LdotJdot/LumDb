using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Utils.ByteUtils;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;


// #if !NATIVE_AOT


namespace LumDbEngine.Element.Manager.Specific
{
    internal partial class DataManager
    {
        internal static void GoThrough
                    <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
                    (DbCache db, ColumnHeader[] headers, DataPage? page, Func<T, bool> action) where T : class, new()
        {
            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable)
                    {
                        var newEntity = ReflectorUtils.Dump(new T(), dataNode.Id, GetValue(db, headers, dataNode.Data));

                        if (!action((T)newEntity))
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


        internal static IEnumerable<T> GetValuesWithIdCondition
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, ColumnHeader[] headers, DataPage? page, Func<T, bool> condition, uint skip, uint limit)
        where T : class, new()
        {
            int currentCount = 0;
            int currentSkip = 0;

            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (!dataNode.IsAvailable) continue;
                    var data = GetValue(db, headers, dataNode.Data);

                    if (data == null) continue;
                    var t = new T();
                    try
                    {
                        ReflectorUtils.Dump(t, dataNode.Id, data);
                    }
                    catch
                    {
                        throw LumException.Raise(LumExceptionMessage.DataReflectionError);
                    }

                    if (!condition(t)) continue;

                    if (skip == 0 || currentSkip >= skip)
                    {
                        if (limit == 0 || currentCount < limit)
                        {
                            currentCount++;
                            yield return t;
                        }
                        else
                        {
                            goto end;
                        }
                    }
                    else
                    {
                        currentSkip++;
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

end:;
        }

        internal static IEnumerable<T> GetValuesWithIdCondition_Backward
         <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, ColumnHeader[] headers, DataPage? page, Func<T, bool> condition, uint skip, uint limit)
        where T : class, new()

        {
            uint initPageId = page?.PageId ?? uint.MaxValue;
            uint nextId = page?.NextPageId ?? uint.MaxValue;

            while (db.IsValidPage(nextId))
            {
                initPageId = nextId;
                nextId = GetNextPageId(db, PageType.Data, initPageId);
            }


            int currentCount = 0;
            int currentSkip = 0;

            page = PageManager.GetPage<DataPage>(db, initPageId);

            while (page != null)
            {
                for (int i = page.MaxDataCount - 1; i >= 0; i--)
                {
                    var dataNode = page.DataNodes[i];

                    if (!dataNode.IsAvailable) continue;
                    var data = GetValue(db, headers, dataNode.Data);

                    if (data == null) continue;
                    var t = new T();

                    ReflectorUtils.Dump(t, dataNode.Id, data);
                    if (!condition(t)) continue;

                    if (skip == 0 || currentSkip >= skip)
                        {
                            if (limit == 0 || currentCount < limit)
                            {
                                currentCount++;
                                yield return t;
                            }
                            else
                            {
                                goto end;
                            }
                        }
                        else
                        {
                            currentSkip++;
                        }
                }

                if (db.IsValidPage(page.PrevPageId))
                {
                    page = PageManager.GetPage<DataPage>(db, page.PrevPageId);
                }
                else
                {
                    page = null;
                }
            }
end:;
        }


    }


}



// #endif