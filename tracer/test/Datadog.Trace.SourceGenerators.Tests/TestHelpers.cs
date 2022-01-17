// <copyright file="TestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.SourceGenerators.TagsListGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Datadog.Trace.SourceGenerators.Tests
{
    internal class TestHelpers
    {
        public static (ImmutableArray<Diagnostic> Diagnostics, string Output) GetGeneratedOutput<T>(params string[] source)
            where T : IIncrementalGenerator, new()
        {
            var syntaxTrees = source.Select(static x => CSharpSyntaxTree.ParseText(x));
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(_ => !_.IsDynamic && !string.IsNullOrWhiteSpace(_.Location))
                .Select(_ => MetadataReference.CreateFromFile(_.Location))
                .Concat(new[] { MetadataReference.CreateFromFile(typeof(T).Assembly.Location) });
            var compilation = CSharpCompilation.Create(
                "Datadog.Trace.Generated",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var originalTreeCount = compilation.SyntaxTrees.Length;
            var generator = new T();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            var trees = outputCompilation.SyntaxTrees.ToList();

            return (diagnostics, trees.Count != originalTreeCount ? trees[trees.Count - 1].ToString() : string.Empty);
        }
    }
}
