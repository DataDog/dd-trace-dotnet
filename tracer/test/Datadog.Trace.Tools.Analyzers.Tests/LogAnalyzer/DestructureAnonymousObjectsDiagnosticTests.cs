// <copyright file="DestructureAnonymousObjectsDiagnosticTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.DestructuringHintCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class DestructureAnonymousObjectsDiagnosticTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Error;

    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.LogAnalyzer
                                               .Diagnostics.DestructureAnonymousObjectsDiagnosticId;

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_NonDestructuredObject(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{logMethod}}("Hello {|#0:{Answer}|}", new { Meh = 42 });
            }
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{logMethod}}("Hello {@Answer}", new { Meh = 42 });
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Property 'Answer' should use destructuring because the argument is an anonymous object");
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }
}
