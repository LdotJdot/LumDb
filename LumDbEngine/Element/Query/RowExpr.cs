using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Query
{
    internal enum RowOp
    {
        Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual,
        And, Or,
        Add, Subtract, Multiply, Divide, Modulo,
        Not, Negate,
        StartsWith, EndsWith, Contains, Length, IsNullOrEmpty, StringEquals,
        ToUpper, ToLower, Trim, TrimStart, TrimEnd, ToStringValue, IsNullOrWhiteSpace,
        IndexOf, LastIndexOf, Replace, Substring,
        Abs, Sign, Floor, Ceiling, Truncate, Round,
        Min, Max, Clamp, Sqrt, Pow,
        Sin, Cos, Tan, Log, Log10, Exp,
    }

    internal abstract class RowExpr
    {
        public abstract int SideMask { get; }

        public abstract object? Eval(ref JoinRow row);

        public object? Eval(ref RowView row, uint id)
        {
            var jr = new JoinRow(0, ref row, id);
            return Eval(ref jr);
        }

        public bool EvalBool(ref JoinRow row)
        {
            var v = Eval(ref row);
            if (v is bool b)
                return b;
            throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} Predicate must return bool.");
        }

        public bool EvalBool(ref RowView row, uint id)
        {
            var jr = new JoinRow(0, ref row, id);
            return EvalBool(ref jr);
        }

        public static RowExpr? And(RowExpr? a, RowExpr? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return new BinaryRowExpr(RowOp.And, a, b);
        }
    }

    internal sealed class ConstExpr : RowExpr
    {
        public ConstExpr(object? value) => Value = value;
        public object? Value { get; }
        public override int SideMask => 0;
        public override object? Eval(ref JoinRow row) => Value;
    }

    internal sealed class ColumnExpr : RowExpr
    {
        public ColumnExpr(int ordinal, DbValueType type) : this(0, ordinal, type)
        {
        }

        public ColumnExpr(int side, int ordinal, DbValueType type)
        {
            Side = side;
            Ordinal = ordinal;
            Type = type;
        }

        public int Side { get; }
        public int Ordinal { get; }
        public DbValueType Type { get; }
        public override int SideMask => 1 << Side;

        public override object? Eval(ref JoinRow row) => row.ReadColumn(Side, Ordinal, Type);

        internal static object? ReadColumn(ref RowView row, int ordinal, DbValueType type) => type switch
        {
            DbValueType.Bool => row.GetBool(ordinal),
            DbValueType.Byte => row.GetByte(ordinal),
            DbValueType.Int => row.GetInt(ordinal),
            DbValueType.UInt => row.GetUInt(ordinal),
            DbValueType.Long => row.GetLong(ordinal),
            DbValueType.ULong => row.GetULong(ordinal),
            DbValueType.Float => row.GetFloat(ordinal),
            DbValueType.Double => row.GetDouble(ordinal),
            DbValueType.Decimal => row.GetDecimal(ordinal),
            DbValueType.DateTimeUTC => row.GetDateTimeUtc(ordinal),
            DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar => row.GetString(ordinal),
            DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar => row.GetBytes(ordinal),
            _ => throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {type}"),
        };
    }

    internal sealed class RowIdExpr : RowExpr
    {
        public static RowIdExpr Instance { get; } = new(0);

        public RowIdExpr(int side) => Side = side;

        public int Side { get; }
        public override int SideMask => 1 << Side;
        public override object? Eval(ref JoinRow row) => row.Id(Side);
    }

    internal sealed class UnaryRowExpr : RowExpr
    {
        public UnaryRowExpr(RowOp op, RowExpr operand)
        {
            Op = op;
            Operand = operand;
        }

        public RowOp Op { get; }
        public RowExpr Operand { get; }
        public override int SideMask => Operand.SideMask;

        public override object? Eval(ref JoinRow row)
        {
            var v = Operand.Eval(ref row);
            return Op switch
            {
                RowOp.Not => !IsTrue(v),
                RowOp.Negate => RowValue.Negate(v),
                RowOp.Length => v is string s ? s.Length : 0,
                RowOp.IsNullOrEmpty => string.IsNullOrEmpty(v as string),
                _ => throw LumException.Raise(LumExceptionMessage.UnsupportedLinq),
            };
        }

        internal static bool IsTrue(object? v) => v is bool b && b;
    }

    internal sealed class BinaryRowExpr : RowExpr
    {
        public BinaryRowExpr(RowOp op, RowExpr left, RowExpr right)
        {
            Op = op;
            Left = left;
            Right = right;
        }

        public RowOp Op { get; }
        public RowExpr Left { get; }
        public RowExpr Right { get; }
        public override int SideMask => Left.SideMask | Right.SideMask;

        public override object? Eval(ref JoinRow row)
        {
            if (Op == RowOp.And)
                return Left.EvalBool(ref row) && Right.EvalBool(ref row);
            if (Op == RowOp.Or)
                return Left.EvalBool(ref row) || Right.EvalBool(ref row);

            var l = Left.Eval(ref row);
            var r = Right.Eval(ref row);
            return Op switch
            {
                RowOp.Equal => RowValue.Compare(l, r) == 0,
                RowOp.NotEqual => RowValue.Compare(l, r) != 0,
                RowOp.LessThan => RowValue.Compare(l, r) < 0,
                RowOp.LessThanOrEqual => RowValue.Compare(l, r) <= 0,
                RowOp.GreaterThan => RowValue.Compare(l, r) > 0,
                RowOp.GreaterThanOrEqual => RowValue.Compare(l, r) >= 0,
                RowOp.Add => RowValue.Add(l, r),
                RowOp.Subtract => RowValue.Sub(l, r),
                RowOp.Multiply => RowValue.Mul(l, r),
                RowOp.Divide => RowValue.Div(l, r),
                RowOp.Modulo => RowValue.Mod(l, r),
                RowOp.StartsWith => Str(l) is string ls && Str(r) is string rs && ls.StartsWith(rs, StringComparison.Ordinal),
                RowOp.EndsWith => Str(l) is string le && Str(r) is string re && le.EndsWith(re, StringComparison.Ordinal),
                RowOp.Contains => Str(l) is string lc && Str(r) is string rc && lc.Contains(rc, StringComparison.Ordinal),
                RowOp.StringEquals => Str(l) is string a && Str(r) is string b && string.Equals(a, b, StringComparison.Ordinal),
                _ => throw LumException.Raise(LumExceptionMessage.UnsupportedLinq),
            };
        }

        private static string? Str(object? v) => v switch
        {
            string s => s,
            char c => c.ToString(),
            _ => null,
        };
    }

    internal sealed class CallRowExpr : RowExpr
    {
        public CallRowExpr(RowOp op, params RowExpr[] args)
        {
            Op = op;
            Args = args;
            var mask = 0;
            foreach (var a in args)
                mask |= a.SideMask;
            SideMask = mask;
        }

        public RowOp Op { get; }
        public RowExpr[] Args { get; }
        public override int SideMask { get; }

        public override object? Eval(ref JoinRow row)
        {
            var n = Args.Length;
            var a0 = n > 0 ? Args[0].Eval(ref row) : null;
            var a1 = n > 1 ? Args[1].Eval(ref row) : null;
            var a2 = n > 2 ? Args[2].Eval(ref row) : null;

            return Op switch
            {
                RowOp.ToUpper => AsString(a0)?.ToUpperInvariant(),
                RowOp.ToLower => AsString(a0)?.ToLowerInvariant(),
                RowOp.Trim => AsString(a0)?.Trim(),
                RowOp.TrimStart => AsString(a0)?.TrimStart(),
                RowOp.TrimEnd => AsString(a0)?.TrimEnd(),
                RowOp.ToStringValue => a0 is null ? "" : Convert.ToString(a0, CultureInfo.InvariantCulture),
                RowOp.IsNullOrWhiteSpace => string.IsNullOrWhiteSpace(a0 as string),
                RowOp.IndexOf => IndexOf(a0, a1),
                RowOp.LastIndexOf => LastIndexOf(a0, a1),
                RowOp.Replace => Replace(a0, a1, a2),
                RowOp.Substring => Substring(a0, a1, n > 2 ? a2 : null),
                RowOp.Abs => Abs(a0),
                RowOp.Sign => Math.Sign(RowValue.ToDouble(a0)),
                RowOp.Floor => Math.Floor(RowValue.ToDecimal(a0)),
                RowOp.Ceiling => Math.Ceiling(RowValue.ToDecimal(a0)),
                RowOp.Truncate => Math.Truncate(RowValue.ToDecimal(a0)),
                RowOp.Round => n > 1
                    ? Math.Round(RowValue.ToDecimal(a0), RowValue.ToInt(a1))
                    : Math.Round(RowValue.ToDecimal(a0)),
                RowOp.Min => RowValue.Compare(a0, a1) <= 0 ? a0 : a1,
                RowOp.Max => RowValue.Compare(a0, a1) >= 0 ? a0 : a1,
                RowOp.Clamp => Clamp(a0, a1, a2),
                RowOp.Sqrt => Domain(RowValue.ToDouble(a0), static x => x < 0 ? null : Math.Sqrt(x)),
                RowOp.Pow => Domain(Math.Pow(RowValue.ToDouble(a0), RowValue.ToDouble(a1))),
                RowOp.Sin => Domain(Math.Sin(RowValue.ToDouble(a0))),
                RowOp.Cos => Domain(Math.Cos(RowValue.ToDouble(a0))),
                RowOp.Tan => Domain(Math.Tan(RowValue.ToDouble(a0))),
                RowOp.Log => Domain(RowValue.ToDouble(a0), static x => x <= 0 ? null : Math.Log(x)),
                RowOp.Log10 => Domain(RowValue.ToDouble(a0), static x => x <= 0 ? null : Math.Log10(x)),
                RowOp.Exp => Domain(Math.Exp(RowValue.ToDouble(a0))),
                _ => throw LumException.Raise(LumExceptionMessage.UnsupportedLinq),
            };
        }

        private static string? AsString(object? v) => v switch
        {
            null => null,
            string s => s,
            char c => c.ToString(),
            _ => Convert.ToString(v, CultureInfo.InvariantCulture),
        };

        private static object IndexOf(object? target, object? needle)
        {
            var s = target as string ?? "";
            if (needle is char ch)
                return s.IndexOf(ch);
            if (needle is string sub)
                return s.IndexOf(sub, StringComparison.Ordinal);
            return -1;
        }

        private static object LastIndexOf(object? target, object? needle)
        {
            var s = target as string ?? "";
            if (needle is char ch)
                return s.LastIndexOf(ch);
            if (needle is string sub)
                return s.LastIndexOf(sub, StringComparison.Ordinal);
            return -1;
        }

        private static object Replace(object? target, object? oldValue, object? newValue)
        {
            var s = target as string ?? "";
            if (oldValue is char oc && newValue is char nc)
                return s.Replace(oc, nc);
            var oldS = oldValue as string ?? "";
            var newS = newValue as string ?? "";
            if (oldS.Length == 0)
                return s;
            return s.Replace(oldS, newS, StringComparison.Ordinal);
        }

        private static object Substring(object? target, object? startObj, object? lenObj)
        {
            var s = target as string ?? "";
            var start = RowValue.ToInt(startObj);
            try
            {
                if (lenObj is null)
                    return s.Substring(start);
                return s.Substring(start, RowValue.ToInt(lenObj));
            }
            catch (ArgumentOutOfRangeException)
            {
                return "";
            }
        }

        private static object Abs(object? v)
        {
            if (v is null) return 0;
            if (v is decimal or float or double)
                return Math.Abs(RowValue.ToDecimal(v));
            return Math.Abs(Convert.ToInt64(v, CultureInfo.InvariantCulture));
        }

        private static object Clamp(object? v, object? min, object? max)
        {
            if (RowValue.Compare(v, min) < 0) return min;
            if (RowValue.Compare(v, max) > 0) return max;
            return v;
        }

        private static object? Domain(double x) => double.IsNaN(x) || double.IsInfinity(x) ? null : x;

        private static object? Domain(double x, Func<double, object?> map)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                return null;
            var y = map(x);
            return y is double d ? Domain(d) : y;
        }
    }

    internal sealed class RowPredExpr : RowExpr
    {
        public RowPredExpr(RowViewPredicate pred, int side = 0)
        {
            _pred = pred;
            Side = side;
        }

        private readonly RowViewPredicate _pred;
        public int Side { get; }
        public override int SideMask => 1 << Side;
        public override object? Eval(ref JoinRow row) => row.Match(Side, _pred);
    }

    internal sealed class EntityPredExpr<T> : RowExpr
    {
        public EntityPredExpr(Func<uint, T?> fromId, Func<T, bool> pred)
        {
            _fromId = fromId;
            _pred = pred;
        }

        private readonly Func<uint, T?> _fromId;
        private readonly Func<T, bool> _pred;
        public override int SideMask => 1;
        public override object? Eval(ref JoinRow row)
        {
            var e = _fromId(row.Id(0));
            return e != null && _pred(e);
        }
    }

    internal sealed class InvokeRowExpr : RowExpr
    {
        public InvokeRowExpr(RowExpr[] args, Func<object?[], object?> invoke)
        {
            Args = args;
            _invoke = invoke;
            var mask = 0;
            foreach (var a in args)
                mask |= a.SideMask;
            SideMask = mask;
        }

        public RowExpr[] Args { get; }
        private readonly Func<object?[], object?> _invoke;
        public override int SideMask { get; }

        public override object? Eval(ref JoinRow row)
        {
            var a = new object?[Args.Length];
            for (int i = 0; i < Args.Length; i++)
                a[i] = Args[i].Eval(ref row);
            return _invoke(a);
        }

        public static bool TryBind(Delegate del, RowExpr[] args, out RowExpr expr)
        {
            expr = null!;
            switch (del)
            {
                case Func<string, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(Str(a[0])));
                    return true;
                case Func<string?, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(a[0] as string));
                    return true;
                case Predicate<string> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(Str(a[0])));
                    return true;
                case Func<int, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToInt(a[0])));
                    return true;
                case Func<uint, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f((uint)RowValue.ToDecimal(a[0])));
                    return true;
                case Func<long, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(Convert.ToInt64(RowValue.ToDecimal(a[0]))));
                    return true;
                case Func<double, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToDouble(a[0])));
                    return true;
                case Func<decimal, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToDecimal(a[0])));
                    return true;
                case Func<bool, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(a[0] is true));
                    return true;
                case Func<string, string, bool> f when args.Length == 2:
                    expr = new InvokeRowExpr(args, a => f(Str(a[0]), Str(a[1])));
                    return true;
                case Func<int, int, bool> f when args.Length == 2:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToInt(a[0]), RowValue.ToInt(a[1])));
                    return true;
                case Func<DateTime, bool> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => a[0] is DateTime dt && f(dt));
                    return true;
                case Func<string, string> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(Str(a[0])));
                    return true;
                case Func<int, int> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToInt(a[0])));
                    return true;
                case Func<double, double> f when args.Length == 1:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToDouble(a[0])));
                    return true;
                case Func<int, int, int> f when args.Length == 2:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToInt(a[0]), RowValue.ToInt(a[1])));
                    return true;
                case Func<string, string, string> f when args.Length == 2:
                    expr = new InvokeRowExpr(args, a => f(Str(a[0]), Str(a[1])));
                    return true;
                case Func<int, int, int, int> f when args.Length == 3:
                    expr = new InvokeRowExpr(args, a => f(RowValue.ToInt(a[0]), RowValue.ToInt(a[1]), RowValue.ToInt(a[2])));
                    return true;
                default:
                    return TryBindAny(del, args, out expr);
            }
        }

        /// <summary>
        /// Any captured Func/delegate: TResult F(T), F(T1,T2), F(T1,T2,T3), … — including bool predicates.
        /// Void (Action) is rejected. AOT: user IL is already live; DynamicInvoke is the open-generic fallback.
        /// </summary>
        [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Captured user delegate is live IL; DynamicInvoke is the arbitrary-callback fallback.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Parameter types come from the captured delegate itself.")]
        private static bool TryBindAny(Delegate del, RowExpr[] args, out RowExpr expr)
        {
            expr = null!;
            var method = del.Method;
            if (method.ReturnType == typeof(void))
                return false;
            var ps = method.GetParameters();
            if (ps.Length != args.Length || ps.Length > 8)
                return false;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType.IsByRef)
                    return false;
            }

            expr = new InvokeRowExpr(args, a =>
            {
                var boxed = new object?[a.Length];
                for (int i = 0; i < a.Length; i++)
                    boxed[i] = Coerce(a[i], ps[i].ParameterType);
                return del.DynamicInvoke(boxed);
            });
            return true;
        }

        [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Generic user method is closed by the C# compiler before we Invoke.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "MethodInfo comes from the expression tree of a live user method.")]
        public static RowExpr FromMethod(MethodInfo method, object? target, RowExpr[] args)
        {
            var ps = method.GetParameters();
            return new InvokeRowExpr(args, a =>
            {
                var boxed = new object?[a.Length];
                for (int i = 0; i < a.Length; i++)
                    boxed[i] = Coerce(a[i], ps[i].ParameterType);
                return method.Invoke(target, boxed);
            });
        }

        private static object? Coerce(object? v, Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (v is null)
                return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t.IsInstanceOfType(v))
                return v;
            if (t == typeof(string))
                return Str(v);
            return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
        }

        private static string Str(object? v) => v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }

    internal static class RowValue
    {
        public static int Compare(object? a, object? b)
        {
            if (a is null && b is null) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            if (a is string sa && b is string sb) return string.CompareOrdinal(sa, sb);
            if (a is DateTime da && b is DateTime db) return da.CompareTo(db);
            if (a is bool ba && b is bool bb) return ba.CompareTo(bb);
            if (a is byte[] aa && b is byte[] bbBytes)
                return aa.AsSpan().SequenceCompareTo(bbBytes);
            if (IsNumber(a) && IsNumber(b))
                return decimal.Compare(ToDecimal(a), ToDecimal(b));
            if (a is IComparable c)
                return c.CompareTo(b);
            return 0;
        }

        public static object Negate(object? v)
        {
            if (v is null) return 0;
            if (IsNumber(v)) return -ToDecimal(v);
            throw LumException.Raise(LumExceptionMessage.UnsupportedLinq);
        }

        public static object Add(object? a, object? b)
        {
            if (a is string || b is string)
                return string.Concat(a, b);
            return ToDecimal(a) + ToDecimal(b);
        }

        public static object Sub(object? a, object? b) => ToDecimal(a) - ToDecimal(b);
        public static object Mul(object? a, object? b) => ToDecimal(a) * ToDecimal(b);
        public static object Div(object? a, object? b) => ToDecimal(a) / ToDecimal(b);
        public static object Mod(object? a, object? b) => Convert.ToInt64(a) % Convert.ToInt64(b);

        public static bool IsNumber(object? v) => v is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

        public static int ToInt(object? v) => Convert.ToInt32(ToDecimal(v), CultureInfo.InvariantCulture);

        public static double ToDouble(object? v) => v switch
        {
            null => 0,
            double d => d,
            float f => f,
            decimal m => (double)m,
            _ => Convert.ToDouble(v, CultureInfo.InvariantCulture),
        };

        public static decimal ToDecimal(object? v) => v switch
        {
            null => 0,
            decimal d => d,
            byte b => b,
            sbyte sb => sb,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => ui,
            long l => l,
            ulong ul => ul,
            float f => (decimal)f,
            double d when double.IsNaN(d) || double.IsInfinity(d) => 0,
            double d => (decimal)d,
            _ => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
        };
    }
}
