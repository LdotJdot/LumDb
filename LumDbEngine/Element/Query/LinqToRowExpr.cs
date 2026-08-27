using System.Linq.Expressions;
using System.Reflection;
using LumDbEngine.Element.Exceptions;

namespace LumDbEngine.Element.Query
{
    internal static class LinqToRowExpr
    {
        public static RowExpr Translate(LambdaExpression lambda, EntityColumnMap map)
            => Translate(lambda, [map]);

        public static RowExpr Translate(LambdaExpression lambda, EntityColumnMap[] maps)
        {
            if (lambda.Parameters.Count != maps.Length)
                throw LumException.Raise(LumExceptionMessage.UnsupportedLinq);
            var roots = new ParameterExpression[lambda.Parameters.Count];
            for (int i = 0; i < roots.Length; i++)
                roots[i] = lambda.Parameters[i];
            return Translate(lambda.Body, roots, maps);
        }

        private static int SideOf(ParameterExpression[] roots, ParameterExpression p)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == p)
                    return i;
            }

            return -1;
        }

        private static RowExpr Translate(Expression node, ParameterExpression[] roots, EntityColumnMap[] maps)
        {
            node = Unwrap(node);

            switch (node)
            {
                case ConstantExpression c:
                    return new ConstExpr(c.Value);

                case ParameterExpression p when SideOf(roots, p) >= 0:
                    throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} The entity parameter itself cannot be used as a value.");

                case MemberExpression m:
                    return TranslateMember(m, roots, maps);

                case UnaryExpression u:
                    return TranslateUnary(u, roots, maps);

                case BinaryExpression b:
                    return TranslateBinary(b, roots, maps);

                case MethodCallExpression call:
                    return TranslateCall(call, roots, maps);

                case InvocationExpression inv:
                    return TranslateInvocation(inv, roots, maps);

                case ConditionalExpression:
                    throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} Ternary is not supported.");

                default:
                    if (!IsRowBound(node, roots) && TryEval(node, out var value))
                        return new ConstExpr(value);
                    throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} ({node})");
            }
        }

        private static RowExpr TranslateMember(MemberExpression m, ParameterExpression[] roots, EntityColumnMap[] maps)
        {
            var expr = m.Expression == null ? null : Unwrap(m.Expression);
            if (expr is ParameterExpression p)
            {
                var side = SideOf(roots, p);
                if (side >= 0)
                {
                    var mapped = maps[side].Resolve(m.Member);
                    return mapped.IsRowId ? new RowIdExpr(side) : new ColumnExpr(side, mapped.Ordinal, mapped.Type);
                }
            }

            if (m.Member.Name == "Length" && m.Expression != null && IsRowBound(m.Expression, roots))
            {
                var target = Translate(m.Expression, roots, maps);
                return new UnaryRowExpr(RowOp.Length, target);
            }

            if (!IsRowBound(m, roots) && TryEval(m, out var value))
                return new ConstExpr(value);

            throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} ({m})");
        }

        private static RowExpr TranslateUnary(UnaryExpression u, ParameterExpression[] roots, EntityColumnMap[] maps)
        {
            if (u.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
            {
                if (!IsRowBound(u.Operand, roots) && TryEval(u, out var folded))
                    return new ConstExpr(folded);
                return Translate(u.Operand, roots, maps);
            }

            if (u.NodeType == ExpressionType.Not)
                return new UnaryRowExpr(RowOp.Not, Translate(u.Operand, roots, maps));

            if (u.NodeType == ExpressionType.Negate || u.NodeType == ExpressionType.NegateChecked)
                return new UnaryRowExpr(RowOp.Negate, Translate(u.Operand, roots, maps));

            throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} ({u})");
        }

        private static RowExpr TranslateBinary(BinaryExpression b, ParameterExpression[] roots, EntityColumnMap[] maps)
        {
            var op = b.NodeType switch
            {
                ExpressionType.Equal => RowOp.Equal,
                ExpressionType.NotEqual => RowOp.NotEqual,
                ExpressionType.LessThan => RowOp.LessThan,
                ExpressionType.LessThanOrEqual => RowOp.LessThanOrEqual,
                ExpressionType.GreaterThan => RowOp.GreaterThan,
                ExpressionType.GreaterThanOrEqual => RowOp.GreaterThanOrEqual,
                ExpressionType.AndAlso => RowOp.And,
                ExpressionType.And => RowOp.And,
                ExpressionType.OrElse => RowOp.Or,
                ExpressionType.Or => RowOp.Or,
                ExpressionType.Add or ExpressionType.AddChecked => RowOp.Add,
                ExpressionType.Subtract or ExpressionType.SubtractChecked => RowOp.Subtract,
                ExpressionType.Multiply or ExpressionType.MultiplyChecked => RowOp.Multiply,
                ExpressionType.Divide => RowOp.Divide,
                ExpressionType.Modulo => RowOp.Modulo,
                _ => throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} ({b.NodeType})"),
            };

            return new BinaryRowExpr(op, Translate(b.Left, roots, maps), Translate(b.Right, roots, maps));
        }

        private static RowExpr TranslateCall(MethodCallExpression call, ParameterExpression[] roots, EntityColumnMap[] maps)
        {
            var method = call.Method;

            if (method.DeclaringType == typeof(string))
            {
                if (method.Name == nameof(string.IsNullOrEmpty) && call.Arguments.Count == 1)
                    return new UnaryRowExpr(RowOp.IsNullOrEmpty, Translate(call.Arguments[0], roots, maps));
                if (method.Name == nameof(string.IsNullOrWhiteSpace) && call.Arguments.Count == 1)
                    return Call(RowOp.IsNullOrWhiteSpace, roots, maps, call.Arguments[0]);
            }

            if (call.Object != null && method.DeclaringType == typeof(string))
            {
                var target = Translate(call.Object, roots, maps);
                if (method.Name == nameof(string.StartsWith) && call.Arguments.Count == 1)
                    return new BinaryRowExpr(RowOp.StartsWith, target, Translate(call.Arguments[0], roots, maps));
                if (method.Name == nameof(string.EndsWith) && call.Arguments.Count == 1)
                    return new BinaryRowExpr(RowOp.EndsWith, target, Translate(call.Arguments[0], roots, maps));
                if (method.Name == nameof(string.Contains) && call.Arguments.Count == 1)
                    return new BinaryRowExpr(RowOp.Contains, target, Translate(call.Arguments[0], roots, maps));
                if (method.Name is nameof(string.Equals) && call.Arguments.Count == 1)
                    return new BinaryRowExpr(RowOp.StringEquals, target, Translate(call.Arguments[0], roots, maps));
                if (method.Name == nameof(string.ToUpper) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.ToUpper, target);
                if (method.Name == nameof(string.ToUpperInvariant) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.ToUpper, target);
                if (method.Name == nameof(string.ToLower) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.ToLower, target);
                if (method.Name == nameof(string.ToLowerInvariant) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.ToLower, target);
                if (method.Name == nameof(string.Trim) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.Trim, target);
                if (method.Name == nameof(string.TrimStart) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.TrimStart, target);
                if (method.Name == nameof(string.TrimEnd) && call.Arguments.Count == 0)
                    return new CallRowExpr(RowOp.TrimEnd, target);
                if (method.Name == nameof(string.IndexOf) && call.Arguments.Count == 1)
                    return new CallRowExpr(RowOp.IndexOf, target, Translate(call.Arguments[0], roots, maps));
                if (method.Name == nameof(string.LastIndexOf) && call.Arguments.Count == 1)
                    return new CallRowExpr(RowOp.LastIndexOf, target, Translate(call.Arguments[0], roots, maps));
                if (method.Name == nameof(string.Replace) && call.Arguments.Count == 2)
                    return new CallRowExpr(RowOp.Replace, target, Translate(call.Arguments[0], roots, maps), Translate(call.Arguments[1], roots, maps));
                if (method.Name == nameof(string.Substring))
                {
                    if (call.Arguments.Count == 1)
                        return new CallRowExpr(RowOp.Substring, target, Translate(call.Arguments[0], roots, maps));
                    if (call.Arguments.Count == 2)
                        return new CallRowExpr(RowOp.Substring, target, Translate(call.Arguments[0], roots, maps), Translate(call.Arguments[1], roots, maps));
                }
            }

            if (call.Object != null && method.Name == nameof(object.ToString) && call.Arguments.Count == 0)
                return new CallRowExpr(RowOp.ToStringValue, Translate(call.Object, roots, maps));

            if (method.DeclaringType == typeof(Math) && TryTranslateMath(call, roots, maps, out var math))
                return math;

            if (method.Name == nameof(object.Equals) && call.Arguments.Count == 1 && call.Object != null)
                return new BinaryRowExpr(RowOp.Equal, Translate(call.Object, roots, maps), Translate(call.Arguments[0], roots, maps));

            if (TryTranslateDelegate(call, roots, maps, out var invoked))
                return invoked;

            if (TryTranslateUserMethod(call, roots, maps, out var userCall))
                return userCall;

            if (!IsRowBound(call, roots) && TryEval(call, out var value))
                return new ConstExpr(value);

            throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} ({call.Method.Name})");
        }

        private static RowExpr TranslateInvocation(InvocationExpression inv, ParameterExpression[] roots, EntityColumnMap[] maps)
        {
            if (TryBindCapturedDelegate(inv.Expression, inv.Arguments, roots, maps, out var expr))
                return expr;
            throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} ({inv})");
        }

        private static bool TryTranslateDelegate(MethodCallExpression call, ParameterExpression[] roots, EntityColumnMap[] maps, out RowExpr expr)
        {
            expr = null!;
            if (call.Object == null || IsRowBound(call.Object, roots))
                return false;
            return TryBindCapturedDelegate(call.Object, call.Arguments, roots, maps, out expr);
        }

        private static bool TryBindCapturedDelegate(
            Expression target,
            IReadOnlyList<Expression> arguments,
            ParameterExpression[] roots,
            EntityColumnMap[] maps,
            out RowExpr expr)
        {
            expr = null!;
            if (IsRowBound(target, roots))
                return false;
            if (!TryEval(target, out var inst) || inst is not Delegate del)
                return false;

            var args = new RowExpr[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
                args[i] = Translate(arguments[i], roots, maps);
            return InvokeRowExpr.TryBind(del, args, out expr);
        }

        /// <summary>
        /// Ordinary C# helpers: int Add(int,int), string Fold(string), or generic Echo&lt;T&gt;(T).
        /// Not BCL (System.*/Microsoft.*) and not instance methods on a column (string.Split).
        /// </summary>
        private static bool TryTranslateUserMethod(
            MethodCallExpression call,
            ParameterExpression[] roots,
            EntityColumnMap[] maps,
            out RowExpr expr)
        {
            expr = null!;
            var method = call.Method;
            if (method.ReturnType == typeof(void))
                return false;
            if (IsFrameworkType(method.DeclaringType) || method.DeclaringType == typeof(string) || method.DeclaringType == typeof(Math))
                return false;
            if (call.Object != null && IsRowBound(call.Object, roots))
                return false;

            object? target = null;
            if (call.Object != null && !TryEval(call.Object, out target))
                return false;

            var ps = method.GetParameters();
            if (ps.Length != call.Arguments.Count || ps.Length > 8)
                return false;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType.IsByRef)
                    return false;
            }

            var args = new RowExpr[ps.Length];
            for (int i = 0; i < ps.Length; i++)
                args[i] = Translate(call.Arguments[i], roots, maps);
            expr = InvokeRowExpr.FromMethod(method, target, args);
            return true;
        }

        private static bool IsFrameworkType(Type? t)
        {
            var ns = t?.Namespace;
            if (string.IsNullOrEmpty(ns))
                return false;
            return ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)
                || ns == "Microsoft" || ns.StartsWith("Microsoft.", StringComparison.Ordinal);
        }

        private static bool TryTranslateMath(MethodCallExpression call, ParameterExpression[] roots, EntityColumnMap[] maps, out RowExpr expr)
        {
            expr = null!;
            var name = call.Method.Name;
            var n = call.Arguments.Count;
            RowOp op;
            if (n == 1)
            {
                op = name switch
                {
                    nameof(Math.Abs) => RowOp.Abs,
                    nameof(Math.Sign) => RowOp.Sign,
                    nameof(Math.Floor) => RowOp.Floor,
                    nameof(Math.Ceiling) => RowOp.Ceiling,
                    nameof(Math.Truncate) => RowOp.Truncate,
                    nameof(Math.Round) => RowOp.Round,
                    nameof(Math.Sqrt) => RowOp.Sqrt,
                    nameof(Math.Sin) => RowOp.Sin,
                    nameof(Math.Cos) => RowOp.Cos,
                    nameof(Math.Tan) => RowOp.Tan,
                    nameof(Math.Log) => RowOp.Log,
                    nameof(Math.Log10) => RowOp.Log10,
                    nameof(Math.Exp) => RowOp.Exp,
                    _ => (RowOp)(-1),
                };
                if ((int)op < 0)
                    return false;
                expr = Call(op, roots, maps, call.Arguments[0]);
                return true;
            }

            if (n == 2)
            {
                if (name == nameof(Math.Round) && call.Arguments[1].Type == typeof(MidpointRounding))
                    return false;
                op = name switch
                {
                    nameof(Math.Min) => RowOp.Min,
                    nameof(Math.Max) => RowOp.Max,
                    nameof(Math.Pow) => RowOp.Pow,
                    nameof(Math.Round) => RowOp.Round,
                    _ => (RowOp)(-1),
                };
                if ((int)op < 0)
                    return false;
                expr = Call(op, roots, maps, call.Arguments[0], call.Arguments[1]);
                return true;
            }

            if (n == 3 && name == nameof(Math.Clamp))
            {
                expr = Call(RowOp.Clamp, roots, maps, call.Arguments[0], call.Arguments[1], call.Arguments[2]);
                return true;
            }

            return false;
        }

        private static CallRowExpr Call(RowOp op, ParameterExpression[] roots, EntityColumnMap[] maps, params Expression[] args)
        {
            var translated = new RowExpr[args.Length];
            for (int i = 0; i < args.Length; i++)
                translated[i] = Translate(args[i], roots, maps);
            return new CallRowExpr(op, translated);
        }

        private static Expression Unwrap(Expression node)
        {
            while (node is UnaryExpression { NodeType: ExpressionType.Quote } q)
                node = q.Operand;
            return node;
        }

        private static bool IsRowBound(Expression node, ParameterExpression[] roots)
        {
            if (node is ParameterExpression p && SideOf(roots, p) >= 0)
                return true;
            return node switch
            {
                ParameterExpression => false,
                MemberExpression m => m.Expression != null && IsRowBound(m.Expression, roots),
                UnaryExpression u => IsRowBound(u.Operand, roots),
                BinaryExpression b => IsRowBound(b.Left, roots) || IsRowBound(b.Right, roots),
                MethodCallExpression c =>
                    (c.Object != null && IsRowBound(c.Object, roots)) || c.Arguments.Any(a => IsRowBound(a, roots)),
                InvocationExpression inv =>
                    IsRowBound(inv.Expression, roots) || inv.Arguments.Any(a => IsRowBound(a, roots)),
                _ => false,
            };
        }

        private static bool TryEval(Expression expr, out object? value)
        {
            try
            {
                switch (expr)
                {
                    case ConstantExpression c:
                        value = c.Value;
                        return true;
                    case MemberExpression m:
                        object? inst = null;
                        if (m.Expression != null)
                        {
                            if (!TryEval(m.Expression, out inst))
                            {
                                value = null;
                                return false;
                            }
                        }

                        value = m.Member switch
                        {
                            FieldInfo f => f.GetValue(inst),
                            PropertyInfo p => p.GetValue(inst),
                            _ => null,
                        };
                        return m.Member is FieldInfo or PropertyInfo;
                    case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u:
                        if (!TryEval(u.Operand, out var inner))
                        {
                            value = null;
                            return false;
                        }

                        var target = Nullable.GetUnderlyingType(u.Type) ?? u.Type;
                        value = inner == null ? null : Convert.ChangeType(inner, target);
                        return true;
                    default:
                        value = null;
                        return false;
                }
            }
            catch
            {
                value = null;
                return false;
            }
        }
    }
}
