// <copyright file="ConfigKeysNoPreprocessorDirsAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.ConfigKeysNoPreprocessorDirsAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

public class ConfigKeysNoPreprocessorDirsAnalyzerTests
{
    private const string DiagnosticId = "DD0011"; // Matches analyzer's DiagnosticId

    [Fact]
    public async Task NoDirectivesInsideConfigurationKeys_ShouldHaveNoDiagnostics()
    {
        var code = """
                   #nullable enable
                   namespace Datadog.Trace.Configuration;

                   public static partial class ConfigurationKeys
                   {
                       public const string A = "DD_SERVICE";
                       public const string B = "DD_ENV";
                   }
                   """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task DirectivesInsideConfigurationKeys_ShouldReportDiagnostics_ForEachDirective()
    {
        var code = """
                   #nullable enable
                   namespace Datadog.Trace.Configuration;

                   public static partial class ConfigurationKeys
                   {
                       {|#0:#if DEBUG|}
                       public const string A = "DD_SERVICE";
                       {|#1:#elif RELEASE|}
                       public const string B = "DD_ENV";
                       {|#2:#else|}
                       public const string C = "DD_VERSION";
                       {|#3:#endif|}

                       {|#4:#region MyRegion|}
                       public const string D = "DD_TRACE_ENABLED";
                       {|#5:#endregion|}

                       {|#6:#pragma warning disable CS0168|}
                       public const string E = "DD_AGENT_HOST";
                       {|#7:#pragma warning restore CS0168|}
                   }
                   """;

        var expected0 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(0)
                        .WithArguments("#if DEBUG");
        var expected1 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(1)
                        .WithArguments("#elif RELEASE");
        var expected2 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(2)
                        .WithArguments("#else");
        var expected3 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(3)
                        .WithArguments("#endif");
        var expected4 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(4)
                        .WithArguments("#region MyRegion");
        var expected5 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(5)
                        .WithArguments("#endregion");
        var expected6 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(6)
                        .WithArguments("#pragma warning disable CS0168");
        var expected7 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                        .WithLocation(7)
                        .WithArguments("#pragma warning restore CS0168");

        await Verifier.VerifyAnalyzerAsync(
            code,
            expected0,
            expected1,
            expected2,
            expected3,
            expected4,
            expected5,
            expected6,
            expected7);
    }

    [Fact]
    public async Task DirectivesInWrongNamespace_ShouldHaveNoDiagnostics()
    {
        var code = """
                   #nullable enable
                   namespace Some.Other.Namespace;

                   public static partial class ConfigurationKeys
                   {
                       #if DEBUG
                       public const string A = "DD_SERVICE";
                       #endif
                   }
                   """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task DirectivesInWrongClass_ShouldHaveNoDiagnostics()
    {
        var code = """
                   #nullable enable
                   namespace Datadog.Trace.Configuration;

                   public static class OtherClass
                   {
                       #if NET8_0_OR_GREATER
                       public const string X = "DD_TRACE_ENABLED";
                       #endif
                   }
                   """;

        await Verifier.VerifyAnalyzerAsync(code);
    }
}
