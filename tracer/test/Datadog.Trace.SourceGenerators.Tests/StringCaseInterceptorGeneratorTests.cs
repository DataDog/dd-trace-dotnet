// <copyright file="StringCaseInterceptorGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.RegularExpressions;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.StringCaseInterception;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

public class StringCaseInterceptorGeneratorTests
{
    private static readonly Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider NetFrameworkOptions =
        TestHelpers.CreateOptionsProvider(("build_property.TargetFrameworkIdentifier", ".NETFramework"));

    private static readonly Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider NetCoreOptions =
        TestHelpers.CreateOptionsProvider(("build_property.TargetFrameworkIdentifier", ".NETCoreApp"));

    private static readonly string[] NetFrameworkPreprocessorSymbols = ["NETFRAMEWORK"];

    [Fact]
    public void InterceptsStringCasingCallsOnNetFramework()
    {
        const string input = """
            public class MyClass
            {
                public void DoWork(string value)
                {
                    var upper = value.ToUpperInvariant();
                    var lower = value.ToLowerInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().HaveCount(1);

        var source = output[0];
        source.Should().Contain("Datadog.Trace.Generated.Interceptors");
        source.Should().Contain("StringUtil.ToUpperInvariant");
        source.Should().Contain("StringUtil.ToLowerInvariant");
        CountInterceptsLocationAttributeUsages(source).Should().Be(2);
    }

    [Fact]
    public void InterceptsConditionalAccessCasingCalls()
    {
        const string input = """
            public class MyClass
            {
                public void DoWork(string value)
                {
                    var upper = value?.ToUpperInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().HaveCount(1);
        CountInterceptsLocationAttributeUsages(output[0]).Should().Be(1);
    }

    [Fact]
    public void DoesNotEmitOnNonNetFrameworkTfm()
    {
        const string input = """
            public class MyClass
            {
                public void DoWork(string value)
                {
                    var upper = value.ToUpperInvariant();
                    var lower = value.ToLowerInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetCoreOptions);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }

    [Fact]
    public void IgnoresNonStringToUpperInvariant()
    {
        const string input = """
            public class Foo
            {
                public string ToUpperInvariant() => "x";
            }

            public class MyClass
            {
                public void DoWork()
                {
                    var result = new Foo().ToUpperInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }

    [Fact]
    public void SkipsCallSitesMarkedWithSkipAttributeOnMethod()
    {
        const string input = """
            namespace Datadog.Trace.Util
            {
                internal sealed class SkipStringCaseInterceptionAttribute : System.Attribute
                {
                }
            }

            public class MyClass
            {
                [Datadog.Trace.Util.SkipStringCaseInterception]
                public void DoWork(string value)
                {
                    var upper = value.ToUpperInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }

    [Fact]
    public void SkipsCallSitesMarkedWithSkipAttributeOnContainingType()
    {
        const string input = """
            namespace Datadog.Trace.Util
            {
                internal sealed class SkipStringCaseInterceptionAttribute : System.Attribute
                {
                }
            }

            [Datadog.Trace.Util.SkipStringCaseInterception]
            public class MyClass
            {
                public void DoWork(string value)
                {
                    var upper = value.ToUpperInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }

    [Fact]
    public void SkipsSelfRecursiveCallsInsideTheHelperItself()
    {
        // Belt-and-braces guard: even without the [SkipStringCaseInterception] attribute, a call inside
        // the type the interceptor delegates to must never be intercepted, or it would recurse forever.
        // The real StringUtil lives in namespace System (see StringUtil.cs), which is what the generator's
        // guard matches against, so the mock below must too.
        const string input = """
            namespace System
            {
                internal static class StringUtil
                {
                    public static string ToUpperInvariant(string value) => value.ToUpperInvariant();
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotEmitWhenThereAreNoCallSites()
    {
        const string input = """
            public class MyClass
            {
                public void DoWork(string value)
                {
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<StringCaseInterceptorGenerator, TrackingNames>(
            new[] { input }, assertOutput: true, additionalFiles: null, optionsProvider: NetFrameworkOptions, preprocessorSymbols: NetFrameworkPreprocessorSymbols);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }

    private static int CountInterceptsLocationAttributeUsages(string source)
        => Regex.Matches(source, Regex.Escape("[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(")).Count;
}
