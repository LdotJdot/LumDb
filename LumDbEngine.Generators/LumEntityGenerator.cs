using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LumDbEngine.Generators
{
    [Generator]
    public sealed class LumEntityGenerator : IIncrementalGenerator
    {
        public const string LumEntityAttributeFullName = "LumDbEngine.LumEntityAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var entities = context.SyntaxProvider.ForAttributeWithMetadataName(
                LumEntityAttributeFullName,
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, _) => ctx);

            context.RegisterSourceOutput(entities, static (spc, ctx) => Execute(spc, ctx));
        }

        private static void Execute(SourceProductionContext spc, GeneratorAttributeSyntaxContext ctx)
        {
            var typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;
            var syntax = (TypeDeclarationSyntax)ctx.TargetNode;

            if (!syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.MustBePartial,
                    syntax.Identifier.GetLocation(),
                    typeSymbol.Name));
                return;
            }

            var members = CollectMembers(typeSymbol, spc, syntax);
            if (members is null)
                return;

            var columns = members.Columns;
            if (columns.Count == 0)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.NoMappableMembers,
                    syntax.Identifier.GetLocation(),
                    typeSymbol.Name));
                return;
            }

            var source = Emit(typeSymbol, columns, members.IdProperty);
            spc.AddSource($"{GetFileName(typeSymbol)}.LumEntity.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private sealed class MemberModel
        {
            public List<ColumnModel> Columns { get; } = new();
            public IPropertySymbol? IdProperty { get; set; }
        }

        private sealed class ColumnModel
        {
            public string Name { get; set; } = "";
            public string DbTypeEnum { get; set; } = "";
            public bool IsKey { get; set; }
            public string ClrTypeDisplay { get; set; } = "";
            public string WriteCall { get; set; } = "";
            public string ReadExpr { get; set; } = "";
            public string GetMethodName { get; set; } = "";
            public string GetReturnType { get; set; } = "";
            public string RowViewGetCall { get; set; } = "";
        }

        private static MemberModel? CollectMembers(INamedTypeSymbol type, SourceProductionContext spc, TypeDeclarationSyntax syntax)
        {
            var model = new MemberModel();
            var props = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public
                            && !p.IsStatic
                            && p.GetMethod != null
                            && p.SetMethod != null)
                .ToList();

            foreach (var p in props)
            {
                if (HasAttr(p, "LumDbEngine.IgnoreAttribute"))
                    continue;

                if (HasAttr(p, "LumDbEngine.IdAttribute"))
                {
                    if (p.Type.SpecialType != SpecialType.System_UInt32)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            Descriptors.IdMustBeUInt,
                            p.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                            p.Name));
                        return null;
                    }
                    model.IdProperty = p;
                    continue;
                }

                var status = TryMapColumn(p, spc, syntax, out var col);
                if (status == ColumnMapStatus.Abort)
                    return null;
                if (status == ColumnMapStatus.Skip)
                    continue;
                model.Columns.Add(col!);
            }

            return model;
        }

        private enum ColumnMapStatus { Mapped, Skip, Abort }

        private static ColumnMapStatus TryMapColumn(IPropertySymbol p, SourceProductionContext spc, SyntaxNode syntax, out ColumnModel? col)
        {
            col = null;
            var isKey = HasAttr(p, "LumDbEngine.KeyAttribute");
            var t = p.Type;
            string? dbType = null;
            string write = "";
            string read = "";
            string getRet = "";
            string rowGet = "";

            var hasStrSize = CountStringSizeAttrs(p) > 0;
            var hasBytesSize = CountBytesSizeAttrs(p) > 0;
            if (CountStringSizeAttrs(p) > 1 || CountBytesSizeAttrs(p) > 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.ConflictingAttrs,
                    p.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                    p.Name));
                return ColumnMapStatus.Abort;
            }

            if (hasStrSize && t.SpecialType != SpecialType.System_String)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.AttrTypeMismatch,
                    p.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                    p.Name, "Str* attributes require string"));
                return ColumnMapStatus.Abort;
            }

            if (hasBytesSize && !(t is IArrayTypeSymbol a0 && a0.Rank == 1 && a0.ElementType.SpecialType == SpecialType.System_Byte))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.AttrTypeMismatch,
                    p.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                    p.Name, "Bytes* attributes require byte[]"));
                return ColumnMapStatus.Abort;
            }

            if (t.SpecialType == SpecialType.System_Boolean)
            {
                dbType = "Bool"; write = $"writer.WriteBool(value.{p.Name})"; read = $"row.GetBool({{ord}})"; getRet = "bool"; rowGet = $"row.GetBool({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_Byte)
            {
                dbType = "Byte"; write = $"writer.WriteByte(value.{p.Name})"; read = $"row.GetByte({{ord}})"; getRet = "byte"; rowGet = $"row.GetByte({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_Int32)
            {
                dbType = "Int"; write = $"writer.WriteInt(value.{p.Name})"; read = $"row.GetInt({{ord}})"; getRet = "int"; rowGet = $"row.GetInt({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_UInt32)
            {
                dbType = "UInt"; write = $"writer.WriteUInt(value.{p.Name})"; read = $"row.GetUInt({{ord}})"; getRet = "uint"; rowGet = $"row.GetUInt({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_Int64)
            {
                dbType = "Long"; write = $"writer.WriteLong(value.{p.Name})"; read = $"row.GetLong({{ord}})"; getRet = "long"; rowGet = $"row.GetLong({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_UInt64)
            {
                dbType = "ULong"; write = $"writer.WriteULong(value.{p.Name})"; read = $"row.GetULong({{ord}})"; getRet = "ulong"; rowGet = $"row.GetULong({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_Single)
            {
                dbType = "Float"; write = $"writer.WriteFloat(value.{p.Name})"; read = $"row.GetFloat({{ord}})"; getRet = "float"; rowGet = $"row.GetFloat({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_Double)
            {
                dbType = "Double"; write = $"writer.WriteDouble(value.{p.Name})"; read = $"row.GetDouble({{ord}})"; getRet = "double"; rowGet = $"row.GetDouble({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_Decimal)
            {
                dbType = "Decimal"; write = $"writer.WriteDecimal(value.{p.Name})"; read = $"row.GetDecimal({{ord}})"; getRet = "decimal"; rowGet = $"row.GetDecimal({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_DateTime)
            {
                dbType = "DateTimeUTC"; write = $"writer.WriteDateTimeUtc(value.{p.Name})"; read = $"row.GetDateTimeUtc({{ord}})"; getRet = "global::System.DateTime"; rowGet = $"row.GetDateTimeUtc({{ord}})";
            }
            else if (t.SpecialType == SpecialType.System_String)
            {
                getRet = "string";
                rowGet = $"row.GetString({{ord}})";
                read = $"row.GetString({{ord}})";
                if (HasAttr(p, "LumDbEngine.Str8BAttribute")) { dbType = "Str8B"; write = $"writer.WriteString(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.Str8B)"; }
                else if (HasAttr(p, "LumDbEngine.Str16BAttribute")) { dbType = "Str16B"; write = $"writer.WriteString(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.Str16B)"; }
                else if (HasAttr(p, "LumDbEngine.Str32BAttribute")) { dbType = "Str32B"; write = $"writer.WriteString(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.Str32B)"; }
                else { dbType = "StrVar"; write = $"writer.WriteString(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.StrVar)"; }
            }
            else if (t is IArrayTypeSymbol arr && arr.Rank == 1 && arr.ElementType.SpecialType == SpecialType.System_Byte)
            {
                getRet = "byte[]";
                rowGet = $"row.GetBytes({{ord}})";
                read = $"row.GetBytes({{ord}})";
                if (HasAttr(p, "LumDbEngine.Bytes8BAttribute")) { dbType = "Bytes8"; write = $"writer.WriteBytes(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.Bytes8)"; }
                else if (HasAttr(p, "LumDbEngine.Bytes16BAttribute")) { dbType = "Bytes16"; write = $"writer.WriteBytes(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.Bytes16)"; }
                else if (HasAttr(p, "LumDbEngine.Bytes32BAttribute")) { dbType = "Bytes32"; write = $"writer.WriteBytes(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.Bytes32)"; }
                else { dbType = "BytesVar"; write = $"writer.WriteBytes(value.{p.Name}, global::LumDbEngine.Element.Structure.DbValueType.BytesVar)"; }
            }
            else
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.UnsupportedType,
                    p.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                    p.Name, t.ToDisplayString()));
                return ColumnMapStatus.Skip;
            }

            col = new ColumnModel
            {
                Name = p.Name,
                DbTypeEnum = dbType!,
                IsKey = isKey,
                ClrTypeDisplay = t.ToDisplayString(),
                WriteCall = write,
                ReadExpr = read,
                GetMethodName = "Get" + p.Name,
                GetReturnType = getRet,
                RowViewGetCall = rowGet,
            };
            return ColumnMapStatus.Mapped;
        }

        private static bool HasNonStringSizeAttr(IPropertySymbol p) =>
            HasAttr(p, "LumDbEngine.Bytes8BAttribute") || HasAttr(p, "LumDbEngine.Bytes16BAttribute")
            || HasAttr(p, "LumDbEngine.Bytes32BAttribute") || HasAttr(p, "LumDbEngine.BytesVarAttribute");

        private static int CountStringSizeAttrs(IPropertySymbol p)
        {
            int n = 0;
            if (HasAttr(p, "LumDbEngine.Str8BAttribute")) n++;
            if (HasAttr(p, "LumDbEngine.Str16BAttribute")) n++;
            if (HasAttr(p, "LumDbEngine.Str32BAttribute")) n++;
            if (HasAttr(p, "LumDbEngine.StrVarAttribute")) n++;
            return n;
        }

        private static int CountBytesSizeAttrs(IPropertySymbol p)
        {
            int n = 0;
            if (HasAttr(p, "LumDbEngine.Bytes8BAttribute")) n++;
            if (HasAttr(p, "LumDbEngine.Bytes16BAttribute")) n++;
            if (HasAttr(p, "LumDbEngine.Bytes32BAttribute")) n++;
            if (HasAttr(p, "LumDbEngine.BytesVarAttribute")) n++;
            return n;
        }

        private static bool HasAttr(ISymbol s, string fullName) =>
            s.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == fullName);

        private static string GetFileName(INamedTypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "")
                .Replace('.', '_')
                .Replace('<', '_')
                .Replace('>', '_');

        private static string Emit(INamedTypeSymbol type, List<ColumnModel> columns, IPropertySymbol? idProp)
        {
            var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
            var fq = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var typeKind = type.IsValueType ? "struct" : "class";
            var isRecord = type.IsRecord;
            var keyword = isRecord ? (type.IsValueType ? "record struct" : "record") : typeKind;

            var containingStack = new Stack<INamedTypeSymbol>();
            for (var c = type.ContainingType; c != null; c = c.ContainingType)
                containingStack.Push(c);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using LumDbEngine;");
            sb.AppendLine("using LumDbEngine.Element.Structure;");
            sb.AppendLine();
            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            int nestIndent = ns == null ? 0 : 1;
            foreach (var outer in containingStack)
            {
                var outerKw = outer.IsRecord
                    ? (outer.IsValueType ? "record struct" : "record")
                    : (outer.IsValueType ? "struct" : "class");
                sb.AppendLine($"{Indent(nestIndent)}partial {outerKw} {outer.Name}");
                sb.AppendLine($"{Indent(nestIndent)}{{");
                nestIndent++;
            }

            sb.AppendLine($"{Indent(nestIndent)}partial {keyword} {type.Name} : global::LumDbEngine.ILumEntity<{fq}>");
            sb.AppendLine($"{Indent(nestIndent)}{{");
            nestIndent++;

            void L(string line) => sb.AppendLine($"{Indent(nestIndent)}{line}");

            L($"static {type.Name}()");
            L("{");
            L($"    global::LumDbEngine.LumEntityProvider.Register<{fq}>();");
            L("}");
            L("");

            L("public static class DbOrd");
            L("{");
            for (int i = 0; i < columns.Count; i++)
                L($"    public const int {columns[i].Name} = {i};");
            L("}");
            L("");

            L($"static (string columnName, global::LumDbEngine.Element.Structure.DbValueType type, bool isKey)[] global::LumDbEngine.ILumEntity<{fq}>.DbSchema => DbSchema;");
            L("public static (string columnName, global::LumDbEngine.Element.Structure.DbValueType type, bool isKey)[] DbSchema { get; } = new (string, global::LumDbEngine.Element.Structure.DbValueType, bool)[]");
            L("{");
            foreach (var c in columns)
                L($"    (\"{c.Name}\", global::LumDbEngine.Element.Structure.DbValueType.{c.DbTypeEnum}, {(c.IsKey ? "true" : "false")}),");
            L("};");
            L("");

            L($"static void global::LumDbEngine.ILumEntity<{fq}>.WriteTo(ref global::LumDbEngine.Element.Structure.RowWriter writer, in {fq} value) => WriteTo(ref writer, in value);");
            L($"public static void WriteTo(ref global::LumDbEngine.Element.Structure.RowWriter writer, in {fq} value)");
            L("{");
            foreach (var c in columns)
                L($"    {c.WriteCall};");
            L("}");
            L("");

            L($"static bool global::LumDbEngine.ILumEntity<{fq}>.TryReadFrom(global::LumDbEngine.Element.Structure.IDbRow row, out {fq} value) => TryReadFrom(row, out value);");
            L($"public static bool TryReadFrom(global::LumDbEngine.Element.Structure.IDbRow row, out {fq} value)");
            L("{");
            L($"    value = new {fq}();");
            for (int i = 0; i < columns.Count; i++)
            {
                var c = columns[i];
                var expr = c.ReadExpr.Replace("{ord}", i.ToString());
                L($"    value.{c.Name} = {expr};");
            }
            L("    return true;");
            L("}");
            L("");

            L($"static void global::LumDbEngine.ILumEntity<{fq}>.SetDbId(ref {fq} value, uint id) => SetDbId(ref value, id);");
            L($"public static void SetDbId(ref {fq} value, uint id)");
            L("{");
            if (idProp != null)
                L($"    value.{idProp.Name} = id;");
            L("}");
            L("");

            for (int i = 0; i < columns.Count; i++)
            {
                var c = columns[i];
                var call = c.RowViewGetCall.Replace("{ord}", $"DbOrd.{c.Name}");
                L($"public static {c.GetReturnType} {c.GetMethodName}(ref global::LumDbEngine.Element.Structure.RowView row) => {call};");
            }

            nestIndent--;
            sb.AppendLine($"{Indent(nestIndent)}}}");
            while (nestIndent > (ns == null ? 0 : 1))
            {
                nestIndent--;
                sb.AppendLine($"{Indent(nestIndent)}}}");
            }

            if (ns != null)
                sb.AppendLine("}");

            return sb.ToString();
        }

        private static string Indent(int level) => new string(' ', level * 4);
    }

    internal static class Descriptors
    {
        public static readonly DiagnosticDescriptor MustBePartial = new(
            "LUMDB001",
            "LumEntity type must be partial",
            "Type '{0}' marked with [LumEntity] must be declared partial",
            "LumDb",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor UnsupportedType = new(
            "LUMDB002",
            "Unsupported property type skipped",
            "Property '{0}' has unsupported type '{1}' and is skipped (not mapped to a column). Apply [Ignore] to silence this warning",
            "LumDb",
            DiagnosticSeverity.Warning,
            true);

        public static readonly DiagnosticDescriptor IdMustBeUInt = new(
            "LUMDB003",
            "Id property must be uint",
            "Property '{0}' marked with [Id] must be of type uint",
            "LumDb",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor AttrTypeMismatch = new(
            "LUMDB004",
            "Attribute type mismatch",
            "Property '{0}': {1}",
            "LumDb",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor NoMappableMembers = new(
            "LUMDB005",
            "No mappable members",
            "Type '{0}' has no mappable public properties for LumEntity",
            "LumDb",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor ConflictingAttrs = new(
            "LUMDB006",
            "Conflicting size attributes",
            "Property '{0}' has conflicting Str*/Bytes* attributes",
            "LumDb",
            DiagnosticSeverity.Error,
            true);
    }
}
