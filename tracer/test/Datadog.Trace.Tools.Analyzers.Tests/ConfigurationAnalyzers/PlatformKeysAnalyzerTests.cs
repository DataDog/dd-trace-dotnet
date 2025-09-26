// <copyright file="PlatformKeysAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.PlatformKeysAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

public class PlatformKeysAnalyzerTests
{
    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.PlatformKeysAnalyzer.DiagnosticId;

    [Fact]
    public async Task ValidPlatformKeysAndEdgeCasesShouldNotHaveDiagnostics()
    {
        var code = """
                   #nullable enable
                   namespace Datadog.Trace.Configuration;

                   internal static partial class PlatformKeys
                   {
                       // Valid platform keys
                       public const string ValidKey1 = "CORECLR_PROFILER_PATH";
                       public const string ValidKey2 = "AWS_LAMBDA_FUNCTION_NAME";
                       public const string ValidKey3 = "WEBSITE_SITE_NAME";
                       
                       // Non-const fields should be ignored
                       public static readonly string ReadOnlyField = "DD_TRACE_ENABLED";
                       public static string StaticField = "OTEL_SERVICE_NAME";
                       
                       // Non-string constants should be ignored
                       public const int IntConstant = 42;
                       public const bool BoolConstant = true;
                       
                       // Edge cases - prefixes in middle/end should NOT trigger
                       public const string OtelButNotPrefix = "SOMETHING_OTEL_VALUE";
                       public const string DdButNotPrefix = "SOMETHING_DD_VALUE";
                       
                       internal class Aws
                       {
                           public const string FunctionName = "AWS_LAMBDA_FUNCTION_NAME";
                           public const string Region = "AWS_REGION";
                       }
                   }
                   """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Theory]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES", "OTEL")]     // Uppercase
    [InlineData("otel_service_name", "OTEL")]           // Lowercase (case insensitive)
    [InlineData("Otel_Exporter_Endpoint", "OTEL")]     // Mixed case
    [InlineData("DD_TRACE_ENABLED", "DD_")]             // Uppercase
    [InlineData("dd_agent_host", "DD_")]                // Lowercase (case insensitive)
    [InlineData("Dd_Version", "DD_")]                   // Mixed case
    [InlineData("_DD_TRACE_DEBUG", "_DD_")]             // Uppercase
    [InlineData("_dd_profiler_enabled", "_DD_")]        // Lowercase (case insensitive)
    [InlineData("_Dd_Test_Config", "_DD_")]             // Mixed case
    public async Task InvalidPlatformKeysConstantsShouldHaveDiagnostics(string invalidValue, string expectedPrefix)
    {
        var code = $$"""
                     #nullable enable
                     namespace Datadog.Trace.Configuration;

                     internal static partial class PlatformKeys
                     {
                         public const string {|#0:InvalidKey|} = "{{invalidValue}}";
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithArguments(invalidValue, expectedPrefix);

        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task MultipleInvalidConstantsIncludingNestedClassesShouldHaveMultipleDiagnostics()
    {
        var code = """
                   #nullable enable
                   namespace Datadog.Trace.Configuration;

                   internal static partial class PlatformKeys
                   {
                       public const string {|#0:InvalidOtelKey|} = "OTEL_SERVICE_NAME";
                       public const string ValidKey = "AWS_LAMBDA_FUNCTION_NAME";
                       public const string {|#1:InvalidDdKey|} = "dd_trace_enabled";
                       
                       internal class TestPlatform
                       {
                           public const string {|#2:InvalidInternalKey|} = "_DD_PROFILER_ENABLED";
                           public const string ValidNestedKey = "WEBSITE_SITE_NAME";
                       }
                   }
                   """;

        var expected1 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(0)
                       .WithArguments("OTEL_SERVICE_NAME", "OTEL");

        var expected2 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(1)
                       .WithArguments("dd_trace_enabled", "DD_");

        var expected3 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                       .WithLocation(2)
                       .WithArguments("_DD_PROFILER_ENABLED", "_DD_");

        await Verifier.VerifyAnalyzerAsync(code, expected1, expected2, expected3);
    }

    [Fact]
    public async Task DifferentNamespaceAndClassNameShouldNotHaveDiagnostics()
    {
        var code = """
                   #nullable enable
                   namespace SomeOther.Namespace
                   {
                       internal static partial class PlatformKeys
                       {
                           public const string ShouldNotBeAnalyzed = "DD_TRACE_ENABLED";
                       }
                   }

                   namespace Datadog.Trace.Configuration
                   {
                       internal static partial class ConfigurationKeys
                       {
                           public const string AlsoNotAnalyzed = "OTEL_SERVICE_NAME";
                       }
                   }
                   """;

        await Verifier.VerifyAnalyzerAsync(code);
    }
}
