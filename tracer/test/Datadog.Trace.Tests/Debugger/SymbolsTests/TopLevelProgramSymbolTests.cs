// <copyright file="TopLevelProgramSymbolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class TopLevelProgramSymbolTests
{
    [Theory]
    [CombinatorialData]
    public void ExtractsTopLevelLocalFunctions(bool emitPdb, bool explicitProgram, bool asyncMain, OptimizationLevel optimization)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TopLevelSymbols_" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            var source = """
                         using System;
                         using System.Runtime.CompilerServices;
                         using System.Threading.Tasks;

                         GetValue(42);
                         Func<int, int> lambda = static value => value + 1;
                         lambda(42);

                         static int GetValue(int value) => value + 1;
                         static async Task<int> GetValueAsync(int value)
                         {
                             await Task.Yield();
                             return value;
                         }
                         """;
            if (asyncMain)
            {
                source += "\nawait GetValueAsync(42);";
            }

            if (explicitProgram)
            {
                source += "\npartial class Program { }";
            }

            source += """

                      class UserType { public static int OrdinaryMethod(int value) => value; }
                      [CompilerGenerated] class Generated { public static void Hidden() { } }
                      namespace Other { [CompilerGenerated] class Program { public static void Hidden() { } } }
                      """;
            var assemblyPath = Path.Combine(directory, "TopLevelSymbols.dll");
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            var sourcePath = Path.Combine(directory, "Program.cs");
            var compilation = CSharpCompilation.Create(
                "TopLevelSymbols",
                syntaxTrees: [CSharpSyntaxTree.ParseText(source, path: sourcePath, encoding: Encoding.UTF8)],
                references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: optimization));
            using (var peStream = File.Create(assemblyPath))
            using (var pdbStream = emitPdb ? File.Create(pdbPath) : null)
            {
                var result = compilation.Emit(peStream, pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
                result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics));
            }

            // Supply the file location without loading or executing the generated application.
            var assembly = new Mock<Assembly>();
            assembly.SetupGet(a => a.Location).Returns(assemblyPath);
            assembly.SetupGet(a => a.FullName).Returns("TopLevelSymbols");
            using var extractor = SymbolExtractor.Create(assembly.Object);
            extractor.Should().NotBeNull();
            (extractor is SymbolPdbExtractor).Should().Be(emitPdb);

            var program = extractor.GetClassSymbols("Program");
            program.Should().NotBeNull();
            foreach (var name in new[] { "GetValue", "GetValueAsync" })
            {
                var method = program.Value.Scopes.Should().ContainSingle(s => s.ScopeType == ScopeType.Method && s.Name.StartsWith("<<Main>$>g__" + name + "|", StringComparison.Ordinal)).Which;
                method.Symbols.Should().ContainSingle().Which.Name.Should().Be("value");
                method.LanguageSpecifics.Value.Annotations.Should().Contain("static");
                if (emitPdb)
                {
                    method.SourceFile.Should().Be(sourcePath);
                    method.StartLine.Should().BePositive();
                    method.HasInjectibleLines.Should().BeTrue();
                }
            }

            extractor.GetClassSymbols("Generated").Should().BeNull();
            extractor.GetClassSymbols("Other.Program").Should().BeNull();
            extractor.GetClassSymbols().Should().NotContain(s => s.Name.Contains("<>"));
            var ordinary = extractor.GetClassSymbols("UserType");
            ordinary.Should().NotBeNull();
            ordinary.Value.Scopes.Should().Contain(s => s.ScopeType == ScopeType.Method && s.Name == "OrdinaryMethod");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
