// <copyright file="CoverageRewriterCorpusTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Coverage.Collector;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Xunit;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilReaderParameters = Mono.Cecil.ReaderParameters;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Rewrites assemblies produced by the C# and Visual Basic compilers, in Debug and Release, and checks that every
/// rewritten method is accepted by the JIT and that executed lines are reported in the document they belong to.
/// Add a construct here when a compiler shape breaks the rewriter.
/// </summary>
[Collection(nameof(CoverageReporterHandlerCollection))]
public class CoverageRewriterCorpusTests
{
    private const string CSharpCorpus = """
        using System;
        using System.Collections.Generic;
        using System.Diagnostics.CodeAnalysis;
        using System.Threading.Tasks;

        public class ExpressionFinalizer { public static int Count; ~ExpressionFinalizer() => Count++; }

        public class BlockFinalizer { public static int Count; ~BlockFinalizer() { Count++; } }

        public static class Shapes
        {
            public static int WhileAtStart(Func<bool> condition) { var count = 0; while (condition()) { count++; } return count; }

            public static int GotoStart(int value) { start: if (value-- > 0) goto start; return value; }

            public static void LockOnly(object gate) { lock (gate) { } }

            public static void UsingOnly(IDisposable disposable) { using (disposable) { } }

            public static int ForeachList(List<int> values) { var sum = 0; foreach (var value in values) { sum += value; } return sum; }

            public static int Filter(Func<int> action) { try { return action(); } catch (InvalidOperationException ex) when (ex.Message.Length > 3) { return -1; } catch { return -2; } finally { Console.Write(string.Empty); } }

            public static IEnumerable<int> Iterator() { try { yield return 1; } finally { Console.Write(string.Empty); } }

            public static async Task<int> AsyncTry(Func<Task<int>> action) { try { return await action(); } catch { return 0; } }

            public static int Switch(object value) => value switch { int number when number > 0 => number, string text => text.Length, _ => 0 };

            public static unsafe int Fixed(int[] values) { fixed (int* pointer = values) { return *pointer; } }

            public static unsafe int Stackalloc(int length) { int* pointer = stackalloc int[length]; return pointer[0]; }

            public static string Constrained<T>(T value) => value.ToString();

            public static int LocalFunction(int value) { return Twice(value); int Twice(int x) => x * 2; }

            public static Func<int, int> Lambda(int offset) => x => x + offset;

            public static int Checked(int a, int b) => checked(a + b);
        }

        public struct ValueWithConstructors
        {
            public int Value;

            public ValueWithConstructors(int value) { Value = value; }

            public ValueWithConstructors(string text) : this(text.Length) { }
        }

        public class BaseWithArgument { public BaseWithArgument(int value) { } }

        public class DerivedWithInitializer : BaseWithArgument
        {
            private int _field = Seed();

            public DerivedWithInitializer(int value) : base(value + Seed()) { _field++; }

            private static int Seed() => 1;
        }

        public class WithStaticConstructor { public static readonly int Value; static WithStaticConstructor() { Value = 5; } }

        public partial class PartialHost
        {
            public PartialHost()
            {
                Console.Write(string.Empty); // executed: ProbePartialConstructor
            }
        }

        public static class Probes
        {
            public static void ProbeGuard() => Guard("value");

            public static void ProbeEarlyReturn() => EarlyReturn("value");

            public static void ProbeForLoop()
            {
                for (var i = 0; i < 3; i++) // executed: ProbeForLoop
                {
                    Console.Write(string.Empty); // executed: ProbeForLoop
                }
            }

            public static void ProbePartialConstructor() => new PartialHost();

            public static void ProbeLineDirective()
            {
        #line 500 "Mapped.razor"
                Console.Write(string.Empty); // executed: ProbeLineDirective@Mapped.razor:500
        #line default
                Console.Write(string.Empty); // executed: ProbeLineDirective
            }

            public static void ProbeExcludedMethod() => Excluded();

            private static void Guard(string value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value)); // executed: ProbeGuard
                Console.Write(string.Empty); // executed: ProbeGuard
            }

            private static int EarlyReturn(string value)
            {
                if (value != null) return 1; return 2; // executed: ProbeEarlyReturn
            }

            [ExcludeFromCodeCoverage]
            private static void Excluded()
            {
                Console.Write(string.Empty); // not-covered: ProbeExcludedMethod
            }
        }
        """;

    private const string CSharpPartialCorpus = """
        using System;

        // Field initializers run as part of every constructor of the type, but their sequence points belong to this file.
        public partial class PartialHost
        {
            private int _initializedHere = Environment.ProcessorCount; // executed: ProbePartialConstructor
        }
        """;

    private const string VisualBasicCorpus = """
        Imports System

        Public Class VisualBasicFinalizer
            Public Shared Count As Integer

            Protected Overrides Sub Finalize()
                Count += 1
            End Sub
        End Class

        Public Module Shapes
        #If HasProjectData Then
            Public Function ResumeNext(action As Func(Of Integer)) As Integer
                On Error Resume Next
                Dim value = action()
                Return value
            End Function
        #End If

            Public Function StaticLocal() As Integer
                Static counter As Integer = Environment.ProcessorCount
                counter += 1
                Return counter
            End Function

            Public Function Filter(action As Func(Of Integer)) As Integer
                Try
                    Return action()
                Catch ex As InvalidOperationException When ex.Message.Length > 2
                    Return -1
                Finally
                    Console.Write(String.Empty)
                End Try
            End Function

            Public Sub DoLoopAtStart(condition As Func(Of Boolean))
                Do While condition()
                Loop
            End Sub
        End Module

        Public Module Probes
            Public Sub ProbeSingleLineIf()
                SingleLineIf(1)
            End Sub

            Private Function SingleLineIf(value As Integer) As Integer
                If value > 0 Then Return 1 Else Return 2 ' executed: ProbeSingleLineIf
            End Function
        End Module
        """;

    // Code outside #ExternalSource blocks has no visible sequence points, so this lives in its own file.
    private const string VisualBasicExternalSourceCorpus = """
        Imports System

        Public Module ExternalSourceProbes
            Public Sub ProbeLineZero()
        #ExternalSource("Mapped.vb", 0)
                Console.Write(String.Empty)
                Console.Write(String.Empty) ' executed: ProbeLineZero@Mapped.vb:1
        #End ExternalSource
            End Sub
        End Module
        """;

    private static readonly Regex MarkerRegex = new(@"(?<kind>executed|not-covered): (?<probe>\w+)(@(?<file>[^:]+):(?<line>\d+))?", RegexOptions.Compiled);

    [Theory]
    [CombinatorialData]
    public void EveryRewrittenMethodIsAcceptedByTheJit(
        [CombinatorialValues(LanguageNames.CSharp, LanguageNames.VisualBasic)] string language,
        bool optimize,
        [CombinatorialValues("LineExecution", "LineCallCount")] string coverageMode)
    {
        using var corpus = RewrittenCorpus.Create(language, optimize, coverageMode);

        PrepareAllMethods(corpus.Original).Should().BeEmpty();
        PrepareAllMethods(corpus.Rewritten).Should().BeEmpty();
    }

    [Theory]
    [CombinatorialData]
    public void ExecutedLinesAreReportedInTheirOwnDocument(
        [CombinatorialValues(LanguageNames.CSharp, LanguageNames.VisualBasic)] string language,
        bool optimize,
        [CombinatorialValues("LineExecution", "LineCallCount")] string coverageMode)
    {
        using var corpus = RewrittenCorpus.Create(language, optimize, coverageMode);
        var probes = corpus.Rewritten.GetTypes()
                           .Where(type => type.Name.EndsWith("Probes", StringComparison.Ordinal))
                           .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                           .ToList();
        probes.Select(probe => probe.Name).Should().BeEquivalentTo(corpus.Markers.Select(marker => marker.Probe).Distinct());

        // Report every probe that misses or misplaces a line, not only the first one.
        using var scope = new AssertionScope();
        foreach (var probe in probes)
        {
            var covered = RunProbe(probe);

            foreach (var file in covered)
            {
                if (corpus.ExecutableLines.TryGetValue(file.Key, out var executableLines))
                {
                    file.Value.Should().BeSubsetOf(executableLines, $"{probe.Name} must report lines of {file.Key} only");
                }
                else
                {
                    corpus.ExecutableLines.Should().ContainKey(file.Key, $"{probe.Name} reported a document that has no sequence points");
                }
            }

            foreach (var marker in corpus.Markers.Where(m => m.Probe == probe.Name))
            {
                var lines = covered.TryGetValue(marker.File, out var fileLines) ? fileLines : new HashSet<int>();
                if (marker.Executed)
                {
                    lines.Should().Contain(marker.Line, $"{probe.Name} executes {marker.File}:{marker.Line}");
                }
                else
                {
                    lines.Should().NotContain(marker.Line, $"{probe.Name} must not report {marker.File}:{marker.Line}");
                }
            }
        }
    }

    private static Dictionary<string, HashSet<int>> RunProbe(MethodInfo probe)
    {
        var previousHandler = CoverageReporter.Handler;
        var handler = new DefaultCoverageEventHandler();
        CoverageReporter.Handler = handler;
        object coverage;
        try
        {
            var session = handler.StartSession("xunit");
            try
            {
                probe.Invoke(null, null);
            }
            finally
            {
                coverage = handler.EndSession(session);
            }
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
        }

        var result = new Dictionary<string, HashSet<int>>();
        if (coverage is null)
        {
            // Sessions without executed lines produce no coverage payload.
            return result;
        }

        foreach (var file in coverage.Should().BeOfType<TestCoverage>().Subject.Files ?? [])
        {
            var lines = new HashSet<int>();
            using var bitmap = new FileBitmap(file.Bitmap!);
            for (var line = 1; line <= bitmap.BitCount; line++)
            {
                if (bitmap.Get(line))
                {
                    lines.Add(line);
                }
            }

            result[Path.GetFileName(file.FileName!)] = lines;
        }

        return result;
    }

    private static List<string> PrepareAllMethods(Assembly assembly)
    {
        var failures = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
            {
                if (method.IsAbstract || method.GetMethodBody() is null)
                {
                    continue;
                }

                try
                {
                    var typeArguments = type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes;
                    var methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : Type.EmptyTypes;
                    var instantiation = typeArguments.Concat(methodArguments).Select(_ => typeof(object).TypeHandle).ToArray();
                    if (instantiation.Length == 0)
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    }
                    else
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle, instantiation);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.FullName}::{method.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        return failures;
    }

    private sealed class Marker
    {
        public Marker(string probe, string file, int line, bool executed)
        {
            Probe = probe;
            File = file;
            Line = line;
            Executed = executed;
        }

        public string Probe { get; }

        public string File { get; }

        public int Line { get; }

        public bool Executed { get; }
    }

    private sealed class RewrittenCorpus : IDisposable
    {
        private readonly string _directory;

        private RewrittenCorpus(string directory, Assembly original, Assembly rewritten, Dictionary<string, HashSet<int>> executableLines, List<Marker> markers)
        {
            _directory = directory;
            Original = original;
            Rewritten = rewritten;
            ExecutableLines = executableLines;
            Markers = markers;
        }

        public Assembly Original { get; }

        public Assembly Rewritten { get; }

        public Dictionary<string, HashSet<int>> ExecutableLines { get; }

        public List<Marker> Markers { get; }

        public static RewrittenCorpus Create(string language, bool optimize, string coverageMode)
        {
            var sources = language == LanguageNames.CSharp
                              ? new[] { ("Corpus.cs", CSharpCorpus), ("PartialHost.cs", CSharpPartialCorpus) }
                              : new[] { ("Corpus.vb", VisualBasicCorpus), ("ExternalSource.vb", VisualBasicExternalSourceCorpus) };

            var directory = Path.Combine(Path.GetTempPath(), "coverage-corpus-" + Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "CoverageCorpus.dll");
                Compile(language, optimize, sources, path);
                var original = Assembly.Load(File.ReadAllBytes(path));
                var executableLines = ReadExecutableLines(path);

                var settings = new CoverageSettings(null, string.Empty);
                settings.TestOptimization.SetCodeCoverageMode(coverageMode);
                new AssemblyProcessor(path, settings).Process();

                var rewritten = Assembly.Load(File.ReadAllBytes(path));
                return new RewrittenCorpus(directory, original, rewritten, executableLines, ReadMarkers(sources));
            }
            catch
            {
                Directory.Delete(directory, recursive: true);
                throw;
            }
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        private static void Compile(string language, bool optimize, (string Path, string Text)[] sources, string assemblyPath)
        {
            if (language == LanguageNames.CSharp)
            {
                TestAssemblyCompiler.CompileCSharp(assemblyPath, optimize, sources);
            }
            else
            {
                TestAssemblyCompiler.CompileVisualBasic(assemblyPath, optimize, GetVisualBasicParseOptions(), sources);
            }
        }

        private static VisualBasicParseOptions GetVisualBasicParseOptions()
        {
#if NETCOREAPP2_1
            // The .NET Core 2.1 Visual Basic runtime lacks ProjectData, which On Error needs.
            return new VisualBasicParseOptions();
#else
            return new VisualBasicParseOptions(preprocessorSymbols: [new KeyValuePair<string, object>("HasProjectData", true)]);
#endif
        }

        private static Dictionary<string, HashSet<int>> ReadExecutableLines(string assemblyPath)
        {
            var lines = new Dictionary<string, HashSet<int>>();
            using var assembly = CecilAssemblyDefinition.ReadAssembly(assemblyPath, new CecilReaderParameters { ReadSymbols = true, InMemory = true });
            foreach (var method in assembly.MainModule.GetTypes().SelectMany(type => type.Methods))
            {
                foreach (var sequencePoint in method.DebugInformation.SequencePoints.Where(sp => !sp.IsHidden))
                {
                    var file = Path.GetFileName(sequencePoint.Document.Url);
                    if (!lines.TryGetValue(file, out var fileLines))
                    {
                        lines[file] = fileLines = new HashSet<int>();
                    }

                    fileLines.Add(sequencePoint.StartLine);
                }
            }

            return lines;
        }

        private static List<Marker> ReadMarkers((string Path, string Text)[] sources)
        {
            var markers = new List<Marker>();
            foreach (var source in sources)
            {
                var lines = source.Text.Split('\n');
                for (var index = 0; index < lines.Length; index++)
                {
                    foreach (Match match in MarkerRegex.Matches(lines[index]))
                    {
                        var file = match.Groups["file"].Success ? match.Groups["file"].Value : source.Path;
                        var line = match.Groups["line"].Success ? int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture) : index + 1;
                        markers.Add(new Marker(match.Groups["probe"].Value, file, line, match.Groups["kind"].Value == "executed"));
                    }
                }
            }

            return markers;
        }
    }
}
