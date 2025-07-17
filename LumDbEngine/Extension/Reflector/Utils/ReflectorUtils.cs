using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;


// #if !NATIVE_AOT

namespace LumDbEngine
{
    internal static class ReflectorUtils
    {      
        internal static object[] Load<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(T t)
        {
           return GetValues<T>(t).Select(o=>o.value).ToArray();
        }


        private static PropertyInfo? GetProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type type,
            Type attributeType)
        {
            var properties = type?.GetProperties(BindingFlags.Public | BindingFlags.Instance)?
                                 .FirstOrDefault(p =>p.CanWrite && p.CanRead && p.GetCustomAttribute(attributeType, false) != null);
            return properties;
        }

        private static IEnumerable<PropertyInfo> GetProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type type,
            params Type[] ignoredAttributeTypes)
        {
            var properties = type?.GetProperties(BindingFlags.Public | BindingFlags.Instance)?
                                 .Where(p =>
                                 {
                                     if (!p.CanWrite || !p.CanRead) return false;

                                     foreach (var attr in ignoredAttributeTypes)
                                     {
                                         if (p.GetCustomAttribute(attr, false) != null)
                                         {
                                             return false;
                                         }
                                     }
                                     return true;
                                 }
                                 );
            return properties ?? [];
        }

        internal static T Dump
         <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (T t, uint id, object[] objects)
        {
            LumException.ThrowIfNull(objects, "The data is null: " + typeof(T).Name);

            var type = typeof(T);
            var properties = GetProperties(type,typeof(IgnoreAttribute),typeof(IdAttribute));

            int index = 0;

            foreach (var p in properties)
            {
                var name = p.Name;
                var dbType = GetDbValueType(p);

                if (dbType == DbValueType.Unknow) continue; // 类型不符合要求时返回

                LumException.ThrowIfTrue(index>=objects.Length,"The type is not consistent with the table: "+ typeof(T).Name);
                try
                {
                    p.SetValue(t, objects[index]);
                }
                catch
                {
                    throw LumException.Raise("Can not set the property value: "+ p.Name + "<=" + objects[index].ToString());
                }
                index++;
            }

            var property_id = GetProperty(type,typeof(IdAttribute));

            if(property_id != null)
            {
                if (GetDbValueType(property_id) == DbValueType.UInt)
                {
                    try
                    {
                        property_id.SetValue(t, id);
                    }
                    catch
                    {
                        throw LumException.Raise("The uint id value cannot set to the property: " + property_id.Name + "-" + property_id.PropertyType.Name);
                    }
                }
                else
                {
                    throw LumException.Raise("The Id property should be uint type");
                }
            }
            return t;
        }


        internal static (string columnName, object value)[] GetValues
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (T t)
        {
            var type = typeof(T);
            var properties = GetProperties(type, typeof(IgnoreAttribute), typeof(IdAttribute));

            var values = new List<(string columnName, object value)>();

            foreach (var p in properties)
            {
                var name = p.Name;
                var dbType = GetDbValueType(p);

                if (dbType == DbValueType.Unknow) continue; // 类型不符合要求时返回

                var value =  p.GetValue(t);

                LumException.ThrowIfNull(value, "The type is not consistent or null:"+p.Name);
                
                values.Add((name, value!));
            }

            return values.ToArray();
        }

        internal static (string columnName, DbValueType type, bool isKey)[] GetPropertity
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            () where T : class
        {
            var type = typeof(T);
            var properties = GetProperties(type, typeof(IgnoreAttribute), typeof(IdAttribute));
            var values=new List<(string columnName, DbValueType type, bool isKey)>();

            foreach(var p in properties)
            {
                var name=p.Name;
                var dbType = GetDbValueType(p);

                if (dbType == DbValueType.Unknow) continue; // 类型不符合要求时返回

                var isKey = (p.GetCustomAttribute(typeof(KeyAttribute)) != null);
                values.Add((name, dbType, isKey));
            }

            return values.ToArray();
        }

        static DbValueType GetDbValueType(PropertyInfo propertyInfo)
        {
            DbValueType type = DbValueType.Unknow;
            var properType = propertyInfo.PropertyType;

            if(properType == typeof(string) || properType == typeof(String))
            {
                if (propertyInfo.GetCustomAttribute(typeof(Str8BAttribute)) != null)
                {
                    type = DbValueType.Str8B;
                }
                else if(propertyInfo.GetCustomAttribute(typeof(Str16BAttribute)) != null)
                { 
                    type = DbValueType.Str16B;                
                }
                else if(propertyInfo.GetCustomAttribute(typeof(Str32BAttribute)) != null)
                { 
                    type = DbValueType.Str32B;                
                }
                else
                { 
                    type = DbValueType.StrVar;
                }
            }
            else if (properType == typeof(byte[]))
            {
                if (propertyInfo.GetCustomAttribute(typeof(Bytes8BAttribute)) != null)
                {
                    type = DbValueType.Bytes8;
                }
                else if (propertyInfo.GetCustomAttribute(typeof(Bytes16BAttribute)) != null)
                {
                    type = DbValueType.Bytes16;
                }
                else if (propertyInfo.GetCustomAttribute(typeof(Bytes16BAttribute)) != null)
                {
                    type = DbValueType.Bytes32;
                }
                else
                {
                    type = DbValueType.BytesVar;
                }
            }
            else if (properType == typeof(bool))
            {
                type = DbValueType.Bool;
            }
            else if (properType == typeof(int))
            {
                type = DbValueType.Int;
            }
            else if (properType == typeof(uint))
            {
                type = DbValueType.UInt;
            }
            else if (properType == typeof(float))
            {
                type = DbValueType.Float;
            }
            else if (properType == typeof(double))
            {
                type = DbValueType.Double;
            }
            else if (properType == typeof(byte)||properType==typeof(Byte))
            {
                type = DbValueType.Byte;
            }
            else if (properType == typeof(long))
            {
                type = DbValueType.Long;
            }
            else if (properType == typeof(ulong))
            {
                type = DbValueType.ULong;
            }
            else if (properType == typeof(DateTime))
            {
                type = DbValueType.DateTimeUTC;
            }
            else if (properType == typeof(decimal))
            {
                type = DbValueType.Decimal;
            }
            return type;
        }
    }
}

// #endif