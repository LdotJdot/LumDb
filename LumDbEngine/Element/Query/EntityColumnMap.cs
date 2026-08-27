using System.Reflection;
using LumDbEngine.Element.Exceptions;
using LumDbEngine;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Utils.StringUtils;

namespace LumDbEngine.Element.Query
{
    internal readonly struct MappedMember
    {
        public MappedMember(int ordinal, DbValueType type)
        {
            IsRowId = false;
            Ordinal = ordinal;
            Type = type;
        }

        public static MappedMember RowId { get; } = new MappedMember { IsRowId = true };

        public bool IsRowId { get; private init; }
        public int Ordinal { get; }
        public DbValueType Type { get; }
    }

    /// <summary>
    /// Maps entity members to table columns (by name, case-insensitive) or engine row id.
    /// </summary>
    internal sealed class EntityColumnMap
    {
        private readonly Dictionary<string, MappedMember> _byName;

        private EntityColumnMap(Dictionary<string, MappedMember> byName)
        {
            _byName = byName;
        }

        public static EntityColumnMap FromHeaders(ColumnHeader[] headers)
        {
            var byName = new Dictionary<string, MappedMember>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                var name = headers[i].Name.TransformToToString();
                if (string.IsNullOrEmpty(name))
                    continue;
                byName[name] = new MappedMember(i, headers[i].ValueType);
            }

            return new EntityColumnMap(byName);
        }

        public MappedMember Resolve(MemberInfo member)
        {
            if (member.GetCustomAttribute<IdAttribute>() != null)
                return MappedMember.RowId;

            if (_byName.TryGetValue(member.Name, out var mapped))
                return mapped;

            if (IsEngineIdMember(member))
                return MappedMember.RowId;

            throw LumException.Raise(
                $"{LumExceptionMessage.UnsupportedLinq} Member '{member.Name}' is not a table column or [Id].");
        }

        private bool IsEngineIdMember(MemberInfo member)
        {
            if (!string.Equals(member.Name, "Id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(member.Name, "id", StringComparison.Ordinal))
                return false;

            var type = member switch
            {
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                _ => null,
            };
            return type == typeof(uint) || type == typeof(uint?);
        }
    }
}
