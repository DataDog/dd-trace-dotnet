// <copyright file="DatadogTraceObsoleteUsageAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Datadog.Trace.Analyzers.Tests.VerifierWithDatadogTraceReference<
    Datadog.Trace.Analyzers.DatadogTraceObsoleteUsageAnalyzer,
    Datadog.Trace.Analyzers.DatadogTraceObsoleteUsageAnalyzerCodeFixProvider>;

namespace Datadog.Trace.Analyzers.Tests;

public class DatadogTraceObsoleteUsageAnalyzerTests
{
    private const string DiagnosticId = DatadogTraceObsoleteUsageAnalyzer.ObsoleteConstructorDiagnosticId;

    [Fact]
    public async Task EmptySourceShouldNotHaveDiagnostics()
    {
        var test = string.Empty;

        // No diagnostics expected to show up
        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Theory]
    [InlineData("var cls = new TestClass();")]
    [InlineData("var d = new decimal();")]
    public async Task ShouldNotFlagUseOfWrongTypes(string testFragment)
    {
        var code = GetSampleCode(testFragment);

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Theory]
    [InlineData("var x = {|#0:new Tracer()|};", "var x = Tracer.Instance;")]
    [InlineData("var x = {|#0:new Datadog.Trace.Tracer()|};", "var x = Datadog.Trace.Tracer.Instance;")]
    [InlineData("var x = {|#0:new global::Datadog.Trace.Tracer()|};", "var x = global::Datadog.Trace.Tracer.Instance;")]
    public async Task ShouldFlagUseOfObsoleteConstructor(string testFragment, string fixedFragment)
    {
        var code = GetSampleCode(testFragment);
        var fixedCode = GetSampleCode(fixedFragment);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
           .WithLocation(0);
        await Verifier.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    private static string GetSampleCode(string testFragment)
    {
        return $$"""
                 using System;
                 using System.Collections.Generic;
                 using System.Linq;
                 using System.Text;
                 using System.Threading;
                 using System.Threading.Tasks;
                 using System.Diagnostics;
                 using Datadog.Trace;

                 namespace ConsoleApplication1
                 {
                     class TestClass
                     {
                         public void TestMethod()
                         {
                             {{testFragment}}
                         }
                     }
                 }
                 """;
    }
}
