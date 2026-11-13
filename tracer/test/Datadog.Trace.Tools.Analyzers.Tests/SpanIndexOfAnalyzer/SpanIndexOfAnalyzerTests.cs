// <copyright file="SpanIndexOfAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.SpanIndexOfAnalyzer.SpanIndexOfAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.SpanIndexOfAnalyzer.SpanIndexOfCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.SpanIndexOfAnalyzer;

public class SpanIndexOfAnalyzerTests
{
    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.SpanIndexOfAnalyzer.Diagnostics.DiagnosticId;

    [Fact]
    public async Task EmptySourceShouldNotHaveDiagnostics()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task FlagsReadOnlySpanIndexOfGreaterThanOrEqualZero()
    {
        var code = GetTestCode("""
                                bool Contains(System.ReadOnlySpan<char> span)
                                {
                                    return {|#0:span.IndexOf('a') >= 0|};
                                }
                                """);

        var fix = GetTestCode("""
                                bool Contains(System.ReadOnlySpan<char> span)
                                {
                                    return span.Contains('a');
                                }
                                """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(0)
                       .WithArguments("ReadOnlySpan");

        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task FlagsSpanIndexOfNotEqualsNegativeOne()
    {
        var code = GetTestCode("""
                                bool Contains(System.Span<char> span)
                                {
                                    return {|#0:span.IndexOf('a') != -1|};
                                }
                                """);

        var fix = GetTestCode("""
                                bool Contains(System.Span<char> span)
                                {
                                    return span.Contains('a');
                                }
                                """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(0)
                       .WithArguments("Span");

        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task FlagsReadOnlySpanIndexOfEqualsNegativeOneAndNegates()
    {
        var code = GetTestCode("""
                                bool DoesNotContain(System.ReadOnlySpan<char> span)
                                {
                                    return {|#0:span.IndexOf('a') == -1|};
                                }
                                """);

        var fix = GetTestCode("""
                                bool DoesNotContain(System.ReadOnlySpan<char> span)
                                {
                                    return !span.Contains('a');
                                }
                                """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(0)
                       .WithArguments("ReadOnlySpan");

        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task FlagsReadOnlySpanIndexOfLessThanZeroAndNegates()
    {
        var code = GetTestCode("""
                                bool DoesNotContain(System.ReadOnlySpan<char> span)
                                {
                                    return {|#0:span.IndexOf('a') < 0|};
                                }
                                """);

        var fix = GetTestCode("""
                                bool DoesNotContain(System.ReadOnlySpan<char> span)
                                {
                                    return !span.Contains('a');
                                }
                                """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(0)
                       .WithArguments("ReadOnlySpan");

        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task DoesNotFlagWhenIndexIsUsed()
    {
        var code = GetTestCode("""
                                int FindIndex(System.ReadOnlySpan<char> span)
                                {
                                    var index = span.IndexOf('a');
                                    return index >= 0 ? index : -1;
                                }
                                """);

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task DoesNotFlagStringIndexOf()
    {
        // string.IndexOf is already covered by the existing CA2249 analyzer rule.
        var code = GetTestCode("""
                                bool Contains(string value)
                                {
                                    return value.IndexOf('a') >= 0;
                                }
                                """);

        await Verifier.VerifyAnalyzerAsync(code);
    }

    private static string GetTestCode(string testFragment)
    {
        return $$"""
                 using System;

                 namespace ConsoleApplication1
                 {
                     class TestClass
                     {
                         {{testFragment}}
                     }
                 }
                 """;
    }
}
