// <copyright file="ReadmeExampleCompilationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class ReadmeExampleCompilationTests
{
    [Fact]
    public void TheExposureHookExampleCompiles()
    {
        var readme = File.ReadAllText(FindReadme());
        var example = Regex.Match(readme, @"```csharp\r?\n(using System\.Collections\.Concurrent;.*?)```", RegexOptions.Singleline);
        example.Success.Should().BeTrue("the README shows the ExposureHook example");

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                         .Select(path => MetadataReference.CreateFromFile(path));
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var sources = new[] { CSharpSyntaxTree.ParseText(example.Groups[1].Value, parseOptions) }.ToList();
        if (typeof(object).Assembly.GetType("System.Runtime.CompilerServices.IsExternalInit") is null)
        {
            // Records need this type, which .NET Core 3.1 lacks; applications targeting it add the same polyfill.
            sources.Add(CSharpSyntaxTree.ParseText("namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }", parseOptions));
        }

        var compilation = CSharpCompilation.Create(
            "ReadmeExample",
            sources,
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).Should().BeEmpty();
    }

    [Fact]
    public void TheTestsRunOnTheIntendedOpenFeatureVersion()
    {
        var version = typeof(global::OpenFeature.Api).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
#if NET9_0
        version.Should().StartWith("2.14.1", "net9.0 overrides the provider's OpenFeature reference");
#else
        version.Should().StartWith("2.3.0", "the provider builds against OpenFeature 2.3.0");
#endif
    }

    private static string FindReadme()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "Datadog.FeatureFlags.OpenFeature", "README.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("docs/Datadog.FeatureFlags.OpenFeature/README.md was not found above the test output directory.");
    }
}
