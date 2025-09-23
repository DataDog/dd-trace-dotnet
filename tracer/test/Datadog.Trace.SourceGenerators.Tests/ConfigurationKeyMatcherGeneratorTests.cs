// <copyright file="ConfigurationKeyMatcherGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

public class ConfigurationKeyMatcherGeneratorTests
{
    [Fact]
    public void CanGenerateConfigurationKeyMatcher()
    {
        // Read the test configuration file
        var configContent = File.ReadAllText("supported-configurations.json");

        // Create additional text for the configuration file
        var additionalText = new TestHelpers.TestAdditionalText("supported-configurations.json", configContent);

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<ConfigurationKeyMatcherGenerator>(additionalText);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ShouldGenerateEmptyOutputForMissingConfigurationFile()
    {
        // Test without providing the configuration file
        var (diagnostics, output) = GetGeneratedOutputWithAdditionalFiles<ConfigurationKeyMatcherGenerator>();

        // Should generate empty output when no configuration file is provided
        output.Should().BeEmpty();

        // No diagnostics expected - generator should just not produce output
        diagnostics.Should().BeEmpty();
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, string Output) GetGeneratedOutputWithAdditionalFiles<T>(
        string[] sources = null,
        TestHelpers.TestAdditionalText[] additionalFiles = null)
        where T : IIncrementalGenerator, new()
    {
        sources ??= new string[0];
        additionalFiles ??= new TestHelpers.TestAdditionalText[0];

        var syntaxTrees = sources.Select(source => CSharpSyntaxTree.ParseText(source)).ToArray();

        var references = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(T).Assembly.Location) });

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new T().AsSourceGenerator();

        var driver = CSharpGeneratorDriver.Create(
            new[] { generator },
            additionalTexts: additionalFiles);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        var output = runResult.GeneratedTrees.LastOrDefault()?.ToString() ?? string.Empty;

        return (runResult.Diagnostics, output);
    }
}
