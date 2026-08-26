using System;

namespace LumDbEngine
{
    /// <summary>Marks a partial class/struct for LumDb source generation (MemoryPack-style).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class LumEntityAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute { }

    /// <summary>Business key column (secondary index). Multiple keys allowed.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class KeyAttribute : Attribute { }

    /// <summary>Maps engine auto row id (uint). Not a table column.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IdAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Str8BAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Str16BAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Str32BAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class StrVarAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Bytes8BAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Bytes16BAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Bytes32BAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class BytesVarAttribute : Attribute { }
}
