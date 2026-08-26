using System.Collections.Immutable;
using LumDbEngine;
using LumDbEngine.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace UnitTestLumDb.Generator
{
    [TestClass]
    public class LumEntityDiagnosticTests
    {
        private static ImmutableArray<Diagnostic> Run(string source)
            => RunFull(source).diags;

        private static (ImmutableArray<Diagnostic> diags, string generated) RunFull(string source)
        {
            var parse = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13);
            var tree = CSharpSyntaxTree.ParseText(source, parse);
            var refs = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(LumEntityAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            };

            var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (tpa != null)
            {
                foreach (var path in tpa.Split(Path.PathSeparator))
                {
                    if (path.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase))
                        refs.Add(MetadataReference.CreateFromFile(path));
                }
            }

            var compilation = CSharpCompilation.Create(
                "GenTest",
                new[] { tree },
                refs.GroupBy(r => r.Display).Select(g => g.First()),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new LumEntityGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: parse);

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var genDiags);
            var lum = genDiags
                .Where(d => d.Id.StartsWith("LUMDB", StringComparison.Ordinal))
                .ToImmutableArray();
            var generated = string.Join("\n", driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
            return (lum, generated);
        }

        [TestMethod]
        public void LUMDB001_MustBePartial()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public class Bad { public int A { get; set; } }
                """);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB001"), string.Join("; ", diags));
            Assert.IsTrue(diags.First(d => d.Id == "LUMDB001").GetMessage().Contains("partial"));
        }

        [TestMethod]
        public void LUMDB002_UnsupportedList()
        {
            var diags = Run("""
                using LumDbEngine;
                using System.Collections.Generic;
                [LumEntity]
                public partial class Bad { public List<int> Items { get; set; } }
                """);
            var skip = diags.FirstOrDefault(d => d.Id == "LUMDB002");
            Assert.IsNotNull(skip, string.Join("; ", diags));
            Assert.AreEqual(DiagnosticSeverity.Warning, skip.Severity);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB005"), "only skipped members should still fail LUMDB005");
        }

        [TestMethod]
        public void LUMDB002_UnsupportedObjectArray()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad { public string[] Names { get; set; } }
                """);
            var skip = diags.First(d => d.Id == "LUMDB002");
            Assert.AreEqual(DiagnosticSeverity.Warning, skip.Severity);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB005"));
        }

        [TestMethod]
        public void LUMDB003_IdMustBeUInt()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad {
                    [Id] public int Id { get; set; }
                    public int A { get; set; }
                }
                """);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB003"), string.Join("; ", diags));
        }

        [TestMethod]
        public void LUMDB004_StrOnNonString()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad {
                    [Str8B] public int Tag { get; set; }
                }
                """);
            Assert.IsTrue(diags.Any(d => d.Id is "LUMDB004" or "LUMDB002"), string.Join("; ", diags));
        }

        [TestMethod]
        public void LUMDB005_NoMembers()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad { }
                """);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB005"), string.Join("; ", diags));
        }

        [TestMethod]
        public void LUMDB006_ConflictingAttrs()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad {
                    [Str8B][StrVar] public string Name { get; set; }
                }
                """);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB006"), string.Join("; ", diags));
        }

        [TestMethod]
        public void LUMDB002_UnsupportedObject()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad { public object X { get; set; } }
                """);
            var skip = diags.First(d => d.Id == "LUMDB002");
            Assert.AreEqual(DiagnosticSeverity.Warning, skip.Severity);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB005"));
        }

        [TestMethod]
        public void LUMDB002_UnsupportedListByte()
        {
            var diags = Run("""
                using LumDbEngine;
                using System.Collections.Generic;
                [LumEntity]
                public partial class Bad { public List<byte> Bin { get; set; } }
                """);
            var skip = diags.First(d => d.Id == "LUMDB002");
            Assert.AreEqual(DiagnosticSeverity.Warning, skip.Severity);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB005"));
        }

        [TestMethod]
        public void LUMDB002_NestedObject_IsSkipped_RemainingColumnsGenerated()
        {
            var (diags, generated) = RunFull("""
                using LumDbEngine;
                public class Profile { public string Name { get; set; } }
                [LumEntity]
                public partial class Order {
                    public int Status { get; set; }
                    public Profile Customer { get; set; }
                }
                """);
            var skip = diags.Single(d => d.Id == "LUMDB002");
            Assert.AreEqual(DiagnosticSeverity.Warning, skip.Severity);
            Assert.IsTrue(skip.GetMessage().Contains("skipped"));
            Assert.IsFalse(diags.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join("; ", diags));
            Assert.IsTrue(generated.Contains("GetStatus"), generated);
            Assert.IsTrue(generated.Contains("\"Status\""), generated);
            Assert.IsFalse(generated.Contains("Customer"), generated);
        }

        [TestMethod]
        public void Ignore_NestedObject_NoWarning()
        {
            var diags = Run("""
                using LumDbEngine;
                public class Profile { public string Name { get; set; } }
                [LumEntity]
                public partial class Order {
                    public int Status { get; set; }
                    [Ignore] public Profile Customer { get; set; }
                }
                """);
            Assert.AreEqual(0, diags.Length, string.Join("; ", diags));
        }

        [TestMethod]
        public void LUMDB004_BytesOnNonByteArray()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Bad {
                    [Bytes8B] public int Tag { get; set; }
                }
                """);
            Assert.IsTrue(diags.Any(d => d.Id == "LUMDB004"), string.Join("; ", diags));
        }

        [TestMethod]
        public void ValidEntity_NoDiagnostics()
        {
            var diags = Run("""
                using LumDbEngine;
                [LumEntity]
                public partial class Good {
                    [Id] public uint Id { get; set; }
                    [Key] public int Sku { get; set; }
                    public string Note { get; set; }
                }
                """);
            Assert.AreEqual(0, diags.Length, string.Join("; ", diags.Select(d => d.ToString())));
        }
    }
}
