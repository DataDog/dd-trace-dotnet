// <copyright file="ExperimentalApiCompilationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class ExperimentalApiCompilationTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ConsumersMustOptInOnlyForSyncCalls(bool useAsync, bool optIn, bool expectDiagnostic)
    {
        var suppression = optIn ? "#pragma warning disable DDFF001\n" : string.Empty;
        var method = useAsync ? "ResolveBooleanValueAsync" : "ResolveBooleanValue";
        var source = suppression + $$"""
            public static class Consumer
            {
                public static object Evaluate(Datadog.FeatureFlags.OpenFeature.DatadogProvider provider)
                    => provider.{{method}}("flag", false);
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                         .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();

        if (expectDiagnostic)
        {
            Assert.Equal("DDFF001", Assert.Single(diagnostics).Id);
        }
        else
        {
            Assert.Empty(diagnostics);
        }
    }
}
