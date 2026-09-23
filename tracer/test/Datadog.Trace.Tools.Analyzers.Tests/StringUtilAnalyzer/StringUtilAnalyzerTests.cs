// <copyright file="StringUtilAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.StringUtilAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.StringUtilAnalyzer.StringUtilAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.StringUtilAnalyzer.StringUtilCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.StringUtilAnalyzer;

public class StringUtilAnalyzerTests
{
    private const string StringUtilStub = """

        // Stand-in for tracer/src/Datadog.Trace/Util/StringUtil.cs so tests don't need the real assembly.
        namespace System
        {
            internal static class StringUtil
            {
                public static bool IsNullOrEmpty(string? value) => string.IsNullOrEmpty(value);

                public static bool IsNullOrWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value);
            }
        }
        """;

    [Fact]
    public async Task EmptySource_NoDiagnostic()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Theory]
    [InlineData("IsNullOrEmpty")]
    [InlineData("IsNullOrWhiteSpace")]
    public async Task StringIsNullOrX_Diagnostic(string methodName)
    {
        var source = GetTestCode($$"""
            var result = string.{|#0:{{methodName}}}|((value);
            """) + StringUtilStub;

        var fixedSource = GetTestCode($""""
            var result = StringUtil.{methodName}(value);
            """") + StringUtilStub;

        var diagnostic = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithArguments(methodName).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Theory]
    [InlineData("IsNullOrEmpty")]
    [InlineData("IsNullOrWhiteSpace")]
    public async Task StringUtilIsNullOrX_NoDiagnostic(string methodName)
    {
        var source = GetTestCode($$"""
            var result = StringUtil.{{methodName}}(value);
            """) + StringUtilStub;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task OtherTypeIsNullOrEmpty_NoDiagnostic()
    {
        // A same-named method on an unrelated type must not be flagged.
        var source = GetTestCode("""
            var result = MyCollection.IsNullOrEmpty(value);
            """) + """

            internal static class MyCollection
            {
                public static bool IsNullOrEmpty(string value) => value.Length == 0;
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    private static string GetTestCode(string testFragment)
    {
        return $$"""
            using System;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    public void TestMethod(string value)
                    {
                        {{testFragment}}
                    }
                }
            }
            """;
    }
}
