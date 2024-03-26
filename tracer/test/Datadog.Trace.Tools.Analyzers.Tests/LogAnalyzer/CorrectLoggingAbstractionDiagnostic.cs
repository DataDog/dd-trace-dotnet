// <copyright file="CorrectLoggingAbstractionDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class CorrectLoggingAbstractionDiagnostic
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.LogAnalyzer
                                               .Diagnostics.UseDatadogLoggerDiagnosticId;

    [Fact]
    public async Task ShouldNotFlag_CorrectUseOfDatadogLoggingGeneric()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TestClass
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TestClass>();
            public void Main()
            {
                Log.Debug("Hello world");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldNotFlag_CorrectUseOfDatadogLoggingNonGeneric()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TestClass
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestClass));
            public void Main()
            {
                Log.Debug("Hello world");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldFlag_IncorrectUseOfSerilog()
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TestClass
        {
            private static Datadog.Trace.Vendors.Serilog.ILogger Log = null;
            public void Main()
            {
                {|#0:Log.Debug("Hello world")|};
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Incorrect use of Serilog ILogger. Use IDatadogLogger instead.");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }
}
