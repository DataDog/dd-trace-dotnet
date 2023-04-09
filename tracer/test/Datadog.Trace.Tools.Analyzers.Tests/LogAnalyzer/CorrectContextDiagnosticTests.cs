// <copyright file="CorrectContextDiagnosticTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.CorrectContextCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class CorrectContextDiagnosticTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.LogAnalyzer
                                               .Diagnostics.UseCorrectContextualLoggerDiagnosticId;

    [Fact]
    public async Task ShouldNotFlag_Generic_CorrectContext()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<A>();
        }

        class B {}
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldNotFlag_NonGeneric_CorrectContext()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(A));
        }

        class B {}
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldNotFlag_WrongTypeInMethod()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            public void test()
            {
                IDatadogLogger Log = DatadogLogging.GetLoggerFor<B>();
            }
        }

        class B {}
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldNotFlag_MultipleLoggers()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log1 = DatadogLogging.GetLoggerFor<B>();
            private static IDatadogLogger Log2 = DatadogLogging.GetLoggerFor(typeof(C));
        }

        class B {}
        class C {}
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldFlag_Generic_WrongContext()
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<{|#0:B|}>();
        }

        class B {}
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<A>();
        }

        class B {}
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Logger 'Log' should use GetLoggerFor<A>() instead of GetLoggerFor<B>()");
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Fact]
    public async Task ShouldFlag_NonGeneric_WrongContext()
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof({|#0:B|}));
        }

        class B {}
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class A
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(A));
        }

        class B {}
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Logger 'Log' should use GetLoggerFor(typeof(A)) instead of GetLoggerFor(typeof(B))");

        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }
}
