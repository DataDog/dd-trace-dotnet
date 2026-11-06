// <copyright file="SymbolExtractorLocalFunctionsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Xunit;

#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Immutable;
using System.Runtime.Loader;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
#endif

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

/// <summary>
/// Local functions are compiler-generated methods with unspeakable names (e.g. &lt;&lt;Main&gt;$&gt;g__GetProduct|0_0),
/// sometimes declared on compiler-generated types (the synthesized Program of top-level statements, closure classes, state machines).
/// They must be discoverable under the name the user wrote, and a probe on their lines must resolve to the method that runs.
/// The samples are compiled at test time because top-level statements require an executable.
/// </summary>
public class SymbolExtractorLocalFunctionsTest : IDisposable
{
    private const string TopLevelStatementsSource = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        var multiplier = args.Length + 1;
        var factor = 3;
        ENTRY_POINT_KIND
        Console.WriteLine(GetProduct("a", 1));
        Console.WriteLine(Captured("b"));
        Console.WriteLine(Scaled(2));
        Console.WriteLine(GetProductAsync("c").Result);
        foreach (var number in Numbers(2)) { Console.WriteLine(number); }
        Func<int> health = () => 42;
        Func<int> capturedLambda = () => multiplier;
        Func<Task<int>> asyncLambda = async () => { await Task.Yield(); return multiplier; };
        Console.WriteLine(health() + capturedLambda() + asyncLambda().Result + CapturedAsync().Result);
        EXTRA_CALL

        static string GetProduct(string uid, int count)
        {
            var product = uid + count; // body:GetProduct
            return product;
        }

        string Captured(string uid)
        {
            var captured = uid + multiplier; // body:Captured
            return captured;
        }

        int Scaled(int value)
        {
            var scaled = value * factor; // body:Scaled
            return scaled;
        }

        static async Task<string> GetProductAsync(string uid)
        {
            await Task.Yield();
            var product = uid.ToUpperInvariant(); // body:GetProductAsync
            return product;
        }

        static IEnumerable<int> Numbers(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return i * 2; // body:Numbers
            }
        }

        async Task<int> CapturedAsync()
        {
            await Task.Yield();
            var capturedAsync = multiplier + 1; // body:CapturedAsync
            return capturedAsync;
        }

        EXTRA_TYPE
        """;

    private const string ExplicitProgramType = """
        partial class Program
        {
            public static string Ordinary(string uid)
            {
                var ordinary = uid + "!"; // body:Ordinary
                return ordinary;
            }
        }
        """;

    private const string OrdinaryClassSource = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        namespace Samples
        {
            public class Handlers
            {
                private readonly string _prefix = ">";

                public string Handle(string uid)
                {
                    var count = uid.Length;
                    var factor = 3;
                    Func<int> lambda = () =>
                    {
                        var doubled = count * 2; // body:lambda
                        return doubled;
                    };

                    Func<Task<int>> asyncLambda = async () => { await Task.Yield(); return count; };

                    return GetProduct(uid) + Captured("!") + Scaled(2) + GetProductAsync(uid).Result + string.Join(",", Numbers(2)) + UsesThis() + lambda() + asyncLambda().Result + CapturedAsync().Result;

                    static string GetProduct(string id)
                    {
                        var product = id + "?"; // body:GetProduct
                        return product;
                    }

                    string Captured(string suffix)
                    {
                        var captured = _prefix + count + suffix; // body:Captured
                        return captured;
                    }

                    int Scaled(int value)
                    {
                        var scaled = value * factor; // body:Scaled
                        return scaled;
                    }

                    static async Task<string> GetProductAsync(string id)
                    {
                        await Task.Yield();
                        var product = id.ToUpperInvariant(); // body:GetProductAsync
                        return product;
                    }

                    static IEnumerable<int> Numbers(int max)
                    {
                        for (var i = 0; i < max; i++)
                        {
                            yield return i; // body:Numbers
                        }
                    }

                    string UsesThis()
                    {
                        var usesThis = _prefix + uid; // body:UsesThis
                        return usesThis;
                    }

                    async Task<string> CapturedAsync()
                    {
                        await Task.Yield();
                        var capturedAsync = _prefix + count; // body:CapturedAsync
                        return capturedAsync;
                    }
                }

                public string Nested(int a)
                {
                    var count = a + 1;
                    Func<int> outer = () => count;
                    if (a > 0)
                    {
                        var inner = a * 2;
                        Func<int> innerLambda = () => inner + count;
                        return InNestedScope() + innerLambda() + outer();

                        // its closure reaches the captured 'this' through the closure of the parent scope
                        string InNestedScope()
                        {
                            var nested = _prefix + inner + count; // body:InNestedScope
                            return nested;
                        }
                    }

                    return string.Empty;
                }
            }
        }
        """;

    private readonly LineProbeResolver _lineProbeResolver = LineProbeResolver.Create(ImmutableHashSet<string>.Empty, ImmutableHashSet<string>.Empty);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dd-symdb-local-functions-" + Path.GetRandomFileName());
#if NETCOREAPP3_1_OR_GREATER
    private readonly AssemblyLoadContext _loadContext = new(nameof(SymbolExtractorLocalFunctionsTest), isCollectible: true);
#endif

    public SymbolExtractorLocalFunctionsTest()
    {
        Directory.CreateDirectory(_directory);
    }

    public enum Sample
    {
        TopLevelStatements,
        AsyncTopLevelStatements,
        ExplicitPartialProgram,
        OrdinaryClass,
    }

    public void Dispose()
    {
#if NETCOREAPP3_1_OR_GREATER
        _loadContext.Unload();
#endif
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // the loaded sample may still lock its files
        }
    }

    [Theory]
    [CombinatorialData]
    public void LocalFunctionsAreSurfacedUnderTheirSourceName(Sample sample, OptimizationLevel optimization, bool emitPdb)
    {
        var compiled = Compile(sample, optimization, emitPdb);
        var classes = ExtractClasses(compiled.Assembly);

        // DEBUG-4512: no compiler-generated name (closure classes, state machines, lambdas, <Main>$) is uploaded
        AssertNoCompilerGeneratedNames(classes);

        var expectedClass = sample == Sample.OrdinaryClass ? "Samples.Handlers" : "Program";
        classes.Select(c => c.Name).Should().BeEquivalentTo(expectedClass);
        var classScope = classes.Single();

        if (sample != Sample.OrdinaryClass)
        {
            // top-level lambdas (e.g. minimal API handlers written as lambdas) have neither a source name nor a user method declaring them
            classScope.Scopes.Should().OnlyContain(s => s.ScopeType == ScopeType.Method);
        }

        var expectedArgs = GetExpectedArguments(sample);
        var methods = classScope.Scopes.Where(s => s.ScopeType == ScopeType.Method).ToArray();
        methods.Select(m => m.Name).Should().BeEquivalentTo(expectedArgs.Keys);

        foreach (var method in methods)
        {
            var args = method.Symbols?.Where(s => s.SymbolType == SymbolType.Arg).Select(s => $"{s.Name}:{s.Type}") ?? [];
            args.Should().Equal(expectedArgs[method.Name], because: $"{method.Name} should expose the arguments declared in source");

            if (!emitPdb)
            {
                // without a PDB, local functions degrade like any other method: discoverable, but without source location
                method.SourceFile.Should().BeNull();
                method.StartLine.Should().Be(0);
                continue;
            }

            if (!compiled.BodyLines.TryGetValue(method.Name, out var bodyLine))
            {
                continue;
            }

            method.SourceFile.Should().Be(compiled.SourcePath);
            method.StartLine.Should().BeLessOrEqualTo(bodyLine);
            method.EndLine.Should().BeGreaterOrEqualTo(bodyLine);
            method.HasInjectibleLines.Should().BeTrue();
            method.InjectibleLines.Should().Contain(r => r.Start <= bodyLine && bodyLine <= r.End, because: $"a probe can be placed in {method.Name} body");
        }
    }

    [Theory]
    [CombinatorialData]
    public void LocalFunctionLocalsAreSurfaced(Sample sample)
    {
        // Release builds may optimize locals away
        var compiled = Compile(sample, OptimizationLevel.Debug, emitPdb: true);
        var classScope = ExtractClasses(compiled.Assembly).Single();

        var expectedLocals = new Dictionary<string, string>
        {
            ["GetProduct"] = "product",
            ["Captured"] = "captured",
            ["Scaled"] = "scaled",
            ["GetProductAsync"] = "product",
            ["Numbers"] = "i",
            ["CapturedAsync"] = "capturedAsync",
        };

        if (sample == Sample.OrdinaryClass)
        {
            expectedLocals["InNestedScope"] = "nested";
        }

        foreach (var expected in expectedLocals)
        {
            var method = classScope.Scopes.Single(s => s.ScopeType == ScopeType.Method && s.Name == expected.Key);
            var locals = method.Scopes?.Where(s => s.ScopeType == ScopeType.Local).SelectMany(s => s.Symbols ?? []).Select(s => s.Name) ?? [];
            locals.Should().Contain(expected.Value, because: $"{expected.Key} declares it");
        }
    }

    [Theory]
    [CombinatorialData]
    public void LineProbeOnLocalFunctionResolvesToTheMethodThatRuns(Sample sample, OptimizationLevel optimization)
    {
        var compiled = Compile(sample, optimization, emitPdb: true);
        var classScope = ExtractClasses(compiled.Assembly).Single();
        var methodsWithBodyMarker = classScope.Scopes.Where(s => s.ScopeType == ScopeType.Method && compiled.BodyLines.ContainsKey(s.Name)).ToArray();
        methodsWithBodyMarker.Select(m => m.Name).Should().BeEquivalentTo(compiled.BodyLines.Keys.Where(name => name != "lambda"));

        foreach (var method in methodsWithBodyMarker)
        {
            // the UI places the probe on a line of the symbol it found, the tracer resolves the line to the method to instrument
            var resolved = ResolveLineProbe(compiled, method.SourceFile, compiled.BodyLines[method.Name]);

            switch (method.Name)
            {
                case "Ordinary":
                    resolved.Name.Should().Be("Ordinary");
                    break;
                case "GetProductAsync" or "Numbers" or "CapturedAsync":
                    // async and iterator local functions run in the MoveNext method of their state machine
                    resolved.Name.Should().Be("MoveNext");
                    SymbolExtractor.TryGetLocalFunctionName(resolved.DeclaringType!.Name.Substring(1, resolved.DeclaringType.Name.LastIndexOf('>') - 1), out var stateMachineOwner).Should().BeTrue();
                    stateMachineOwner.Should().Be(method.Name);
                    break;
                default:
                    SymbolExtractor.TryGetLocalFunctionName(resolved.Name, out var localFunctionName).Should().BeTrue(because: $"{resolved.Name} should be the local function");
                    localFunctionName.Should().Be(method.Name);
                    break;
            }
        }
    }

    [Theory]
    [CombinatorialData]
    public void LambdasStayPartOfTheMethodDeclaringThem(OptimizationLevel optimization)
    {
        // lambdas have no source name: they are not surfaced as methods, their lines belong to the scope of the method declaring them
        var compiled = Compile(Sample.OrdinaryClass, optimization, emitPdb: true);
        var classScope = ExtractClasses(compiled.Assembly).Single();
        var lambdaLine = compiled.BodyLines["lambda"];

        var handle = classScope.Scopes.Single(s => s.ScopeType == ScopeType.Method && s.Name == "Handle");
        handle.StartLine.Should().BeLessOrEqualTo(lambdaLine);
        handle.EndLine.Should().BeGreaterOrEqualTo(lambdaLine);
        var lambdaScope = handle.Scopes.Single(s => s.ScopeType == ScopeType.Closure && s.InjectibleLines?.Any(r => r.Start <= lambdaLine && lambdaLine <= r.End) == true);
        lambdaScope.Name.Should().Be("Handle");

        // a line probe in the lambda body binds in the sample, see LineProbeResolver for how the containing method is chosen
        ResolveLineProbe(compiled, handle.SourceFile, lambdaLine).Should().NotBeNull();
    }

    [Theory]
    [InlineData("<<Main>$>g__GetProduct|0_2", "GetProduct")]
    [InlineData("<<Main>$>g__Captured|3", "Captured")]
    [InlineData("<Handle>g__Inner|1_1", "Inner")]
    [InlineData("<.ctor>g__Init|0_0", "Init")]
    [InlineData("<N.IFoo.Bar>g__Helper|2_0", "Helper")]
    [InlineData("<Handle>g__X|4", "X")]
    public void TryGetLocalFunctionName_ReturnsTheSourceName(string methodName, string expected)
    {
        SymbolExtractor.TryGetLocalFunctionName(methodName, out var localFunctionName).Should().BeTrue();
        localFunctionName.Should().Be(expected);
    }

    [Theory]
    [InlineData("<<Main>$>b__0_0")]
    [InlineData("<Handle>b__0")]
    [InlineData("<Main>$")]
    [InlineData("<Main>")]
    [InlineData("MoveNext")]
    [InlineData("GetProduct")]
    [InlineData("<>g__GetProduct|0_2")]
    [InlineData("<Main>g__GetProduct")]
    [InlineData("<Main>g__|0_2")]
    [InlineData("g__GetProduct|0_2")]
    [InlineData("<Main>x__GetProduct|0_2")]
    [InlineData("<")]
    [InlineData("|")]
    [InlineData("<>c__DisplayClass0_0")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetLocalFunctionName_RejectsOtherNames(string methodName)
    {
        SymbolExtractor.TryGetLocalFunctionName(methodName, out var localFunctionName).Should().BeFalse();
        localFunctionName.Should().BeNull();
        SymbolExtractor.IsLocalFunctionName(methodName).Should().BeFalse();
    }

    private static Dictionary<string, string[]> GetExpectedArguments(Sample sample)
    {
        if (sample == Sample.OrdinaryClass)
        {
            return new Dictionary<string, string[]>
            {
                ["Handle"] = ["this:Samples.Handlers", "uid:System.String"],
                [".ctor"] = ["this:Samples.Handlers"],
                ["GetProduct"] = ["id:System.String"],
                ["Captured"] = ["this:Samples.Handlers", "suffix:System.String"],
                // shares its closure with local functions capturing 'this'
                ["Scaled"] = ["this:Samples.Handlers", "value:System.Int32"],
                ["GetProductAsync"] = ["id:System.String"],
                ["Numbers"] = ["max:System.Int32"],
                ["UsesThis"] = ["this:Samples.Handlers"],
                ["CapturedAsync"] = ["this:Samples.Handlers"],
                ["Nested"] = ["this:Samples.Handlers", "a:System.Int32"],
                ["InNestedScope"] = ["this:Samples.Handlers"],
            };
        }

        var expected = new Dictionary<string, string[]>
        {
            ["GetProduct"] = ["uid:System.String", "count:System.Int32"],
            ["Captured"] = ["uid:System.String"],
            ["Scaled"] = ["value:System.Int32"],
            ["GetProductAsync"] = ["uid:System.String"],
            ["Numbers"] = ["count:System.Int32"],
            ["CapturedAsync"] = [],
        };

        if (sample == Sample.ExplicitPartialProgram)
        {
            // an explicit Program is a user type: its own members are uploaded like in any other class
            expected["Ordinary"] = ["uid:System.String"];
            expected[".ctor"] = ["this:Program"];
        }

        return expected;
    }

    private static void AssertNoCompilerGeneratedNames(IEnumerable<Trace.Debugger.Symbols.Model.Scope> scopes)
    {
        foreach (var scope in scopes)
        {
            if (scope.ScopeType != ScopeType.Local)
            {
                scope.Name.Should().NotBeNullOrEmpty();
                scope.Name.Should().NotContainAny(["<", ">", "b__", "DisplayClass", "MoveNext", "|"], because: $"{scope.ScopeType} '{scope.Name}' should be a name the user wrote");
            }

            if (scope.Scopes is { } nestedScopes)
            {
                AssertNoCompilerGeneratedNames(nestedScopes);
            }
        }
    }

    private static Trace.Debugger.Symbols.Model.Scope[] ExtractClasses(Assembly assembly)
    {
        using var extractor = SymbolExtractor.Create(assembly);
        extractor.Should().NotBeNull();

        // <Module> is the module's global type, it is not related to local functions
        return extractor!.GetClassSymbols().Where(c => c.Name != "<Module>").ToArray();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string[] paths =
        [
            typeof(object).Assembly.Location,
            typeof(Console).Assembly.Location,
            typeof(Task).Assembly.Location,
            typeof(Enumerable).Assembly.Location,
            Path.Combine(frameworkDirectory, "System.Runtime.dll"),
            Path.Combine(frameworkDirectory, "System.Collections.dll"),
        ];

        return paths.Distinct().Where(File.Exists).Select(path => MetadataReference.CreateFromFile(path));
    }

    private static Dictionary<string, int> GetBodyLines(string source)
    {
        const string marker = "// body:";
        var bodyLines = new Dictionary<string, int>();
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var markerIndex = lines[i].IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                bodyLines[lines[i].Substring(markerIndex + marker.Length).Trim()] = i + 1;
            }
        }

        return bodyLines;
    }

    private MethodBase ResolveLineProbe(CompiledSample compiled, string sourceFile, int line)
    {
        var probe = new LogProbe { Id = Guid.NewGuid().ToString(), Where = new Where { SourceFile = sourceFile, Lines = [line.ToString()] } };
        var result = _lineProbeResolver.TryResolveLineProbe(probe, out var location);

        result.Status.Should().Be(LiveProbeResolveStatus.Bound, because: result.Message);
        location!.Mvid.Should().Be(compiled.Assembly.ManifestModule.ModuleVersionId);
        return compiled.Assembly.ManifestModule.ResolveMethod(location.MethodToken);
    }

    private CompiledSample Compile(Sample sample, OptimizationLevel optimization, bool emitPdb)
    {
        var source = sample switch
        {
            Sample.TopLevelStatements => TopLevelStatementsSource.Replace("ENTRY_POINT_KIND", "// sync entry point").Replace("EXTRA_CALL", string.Empty).Replace("EXTRA_TYPE", string.Empty),
            Sample.AsyncTopLevelStatements => TopLevelStatementsSource.Replace("ENTRY_POINT_KIND", "await Task.Yield();").Replace("EXTRA_CALL", string.Empty).Replace("EXTRA_TYPE", string.Empty),
            Sample.ExplicitPartialProgram => TopLevelStatementsSource.Replace("ENTRY_POINT_KIND", "// sync entry point").Replace("EXTRA_CALL", "Console.WriteLine(Program.Ordinary(\"d\"));").Replace("EXTRA_TYPE", ExplicitProgramType),
            _ => OrdinaryClassSource,
        };

        // a unique name per compilation, LoadFrom returns an already loaded assembly with the same name
        var name = $"LocalFunctionSamples_{sample}_{optimization}_{(emitPdb ? "Pdb" : "NoPdb")}_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}";
        var sourcePath = Path.Combine(_directory, name, "Program.cs");
        var dllPath = Path.Combine(_directory, name, name + ".dll");
        var pdbPath = Path.ChangeExtension(dllPath, ".pdb");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, source, Encoding.UTF8);

        var compilation = CSharpCompilation.Create(
            name,
            syntaxTrees: [CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), path: sourcePath)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(sample == Sample.OrdinaryClass ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication, optimizationLevel: optimization));

        EmitResult emitResult;
        using (var peStream = File.Create(dllPath))
        {
            if (emitPdb)
            {
                using var pdbStream = File.Create(pdbPath);
                emitResult = compilation.Emit(peStream, pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: pdbPath));
            }
            else
            {
                emitResult = compilation.Emit(peStream);
            }
        }

        emitResult.Success.Should().BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

#if NETCOREAPP3_1_OR_GREATER
        var assembly = _loadContext.LoadFromAssemblyPath(dllPath);
#else
        var assembly = Assembly.LoadFrom(dllPath);
#endif
        return new CompiledSample(assembly, sourcePath, GetBodyLines(source));
    }

    private sealed class CompiledSample(Assembly assembly, string sourcePath, Dictionary<string, int> bodyLines)
    {
        public Assembly Assembly { get; } = assembly;

        public string SourcePath { get; } = sourcePath;

        public Dictionary<string, int> BodyLines { get; } = bodyLines;
    }
}
