// <copyright file="EnvironmentGetEnvironmentVariableAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.EnvironmentGetEnvironmentVariableAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

public class EnvironmentGetEnvironmentVariableAnalyzerTests
{
    private const string DD0011 = "DD0011";
    private const string DD0012 = "DD0012";

    [Fact]
    public async Task ValidUsage_WithConfigurationKeysConstant_NoDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Configuration
            {
                internal static partial class ConfigurationKeys { public const string ApiKey = "DD_API_KEY"; }
            }
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C { void M() => EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.ApiKey); }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task ValidUsage_WithNestedConfigurationKeys_NoDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Configuration
            {
                internal static class ConfigurationKeys { public static class CIVisibility { public const string Enabled = "DD_CIVISIBILITY_ENABLED"; } }
            }
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C { void M() => EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.Enabled); }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task ValidUsage_WithPlatformKeys_NoDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Configuration
            {
                internal static class PlatformKeys { public const string AwsLambda = "AWS_LAMBDA_FUNCTION_NAME"; }
            }
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C { void M() => EnvironmentHelpers.GetEnvironmentVariable(Configuration.PlatformKeys.AwsLambda); }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task InvalidUsage_WithHardcodedString_ReportsDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C { void M() => EnvironmentHelpers.GetEnvironmentVariable({|#0:"DD_API_KEY"|}); }
            }
            """;

        var expected = new DiagnosticResult(DD0011, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("DD_API_KEY");

        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task InvalidUsage_WithVariable_ReportsDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C { void M() { var key = "DD_API_KEY"; EnvironmentHelpers.GetEnvironmentVariable({|#0:key|}); } }
            }
            """;

        var expected = new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("key");

        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task InvalidUsage_WithStringInterpolation_ReportsDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C { void M() { var prefix = "DD_"; EnvironmentHelpers.GetEnvironmentVariable({|#0:$"{prefix}API_KEY"|}); } }
            }
            """;

        var expected = new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("$\"{prefix}API_KEY\"");

        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task InvalidUsage_WithConstantFromDifferentClass_ReportsDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                static class MyKeys { public const string ApiKey = "DD_API_KEY"; }
                class C { void M() => EnvironmentHelpers.GetEnvironmentVariable({|#0:MyKeys.ApiKey|}); }
            }
            """;

        var expected = new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("MyKeys.ApiKey");

        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ValidUsage_OtherEnvironmentMethods_NoDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers 
                { 
                    public static string GetEnvironmentVariable(string key) => null;
                    public static void SetEnvironmentVariable(string key, string value) { }
                }
            
                class C { void M() => EnvironmentHelpers.SetEnvironmentVariable("DD_API_KEY", "value"); }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task InvalidUsage_WithParameter_ReportsDiagnostic()
    {
        var code = """
            namespace Datadog.Trace.Util
            {
                internal static class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            
                class C 
                { 
                    void GetEnv(string key) => EnvironmentHelpers.GetEnvironmentVariable({|#0:key|});
                    void Caller() => GetEnv("DD_API_KEY");
                }
            }
            """;

        var expected = new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("key");

        await Verifier.VerifyAnalyzerAsync(code, expected);
    }
}
