// <copyright file="TestAssemblyCompiler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Compiles test fixtures in memory with Roslyn, against the framework the tests run on, into an assembly with a
/// portable PDB next to it, which is what the coverage rewriter expects.
/// </summary>
internal static class TestAssemblyCompiler
{
    public static void CompileCSharp(string assemblyPath, bool optimize, params (string Path, string Text)[] sources)
        => Emit(
            CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(assemblyPath),
                sources.Select(source => CSharpSyntaxTree.ParseText(SourceText.From(source.Text, Encoding.UTF8), new CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest), source.Path)),
                GetFrameworkReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: GetOptimizationLevel(optimize), allowUnsafe: true)),
            assemblyPath);

    public static void CompileVisualBasic(string assemblyPath, bool optimize, VisualBasicParseOptions parseOptions, params (string Path, string Text)[] sources)
        => Emit(
            VisualBasicCompilation.Create(
                Path.GetFileNameWithoutExtension(assemblyPath),
                sources.Select(source => VisualBasicSyntaxTree.ParseText(SourceText.From(source.Text, Encoding.UTF8), parseOptions, source.Path)),
                GetFrameworkReferences(),
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: GetOptimizationLevel(optimize))),
            assemblyPath);

    private static OptimizationLevel GetOptimizationLevel(bool optimize)
        => optimize ? OptimizationLevel.Release : OptimizationLevel.Debug;

    private static void Emit(Compilation compilation, string assemblyPath)
    {
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        using var peStream = File.Create(assemblyPath);
        using var pdbStream = File.Create(pdbPath);
        var result = compilation.Emit(peStream, pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: pdbPath));
        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
    }

    private static IEnumerable<MetadataReference> GetFrameworkReferences()
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
              .Split(Path.PathSeparator)
              .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(Path.GetDirectoryName(path), frameworkDirectory, StringComparison.OrdinalIgnoreCase))
              .Select(path => MetadataReference.CreateFromFile(path));
    }
}
