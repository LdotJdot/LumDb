using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Structure;
using System;
using System.Runtime.InteropServices;


// #if !NATIVE_AOT
namespace LumDbEngine
{
    #region member
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class IgnoreAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class KeyAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property|System.AttributeTargets.Struct)]
    public sealed class IdAttribute : System.Attribute { }
    #endregion

    #region member
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class Str8BAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class Str16BAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class Str32BAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class StrVarAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class Bytes8BAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class Bytes16BAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class Bytes32BAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class BytesVarAttribute : System.Attribute { }
    #endregion

}

// #endif