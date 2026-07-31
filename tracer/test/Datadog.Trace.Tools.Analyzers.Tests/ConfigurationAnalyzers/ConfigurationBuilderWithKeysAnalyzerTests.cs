// <copyright file="ConfigurationBuilderWithKeysAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.ConfigurationBuilderWithKeysAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

public class ConfigurationBuilderWithKeysAnalyzerTests
{
    private const string Dd0007 = "DD0007"; // Hardcoded string literal
    private const string Dd0008 = "DD0008"; // Variable or expression
    private const string Dd0015 = "DD0015";
    private const string Dd0016 = "DD0016";
    private const string SensitiveConfigurationTypes = AnalyzerTestHelper.MinimalRequiredTypes + """
        namespace Datadog.Trace.Configuration
        {
            public static partial class ConfigurationKeys
            {
                public const string ApiKey = "DD_API_KEY";
                public const string ServiceName = "DD_SERVICE";
            }
        }
        namespace Datadog.Trace.Configuration.Telemetry
        {
            public struct ConfigurationBuilder
            {
                public HasKeys WithKeys(string key) => default;

                public struct HasKeys
                {
                    public string AsString() => null;
                    public object AsDictionaryResult() => null;
                    public object AsStringResult(object validator, object converter, bool recordValue) => null;
                    public string AsRedactedString() => null;
                    public object AsRedactedStringResult() => null;
                }
            }
        }
        """;

    [Fact]
    public async Task SensitiveKeyWithAsString_ShouldReportDD0015ButNonSensitiveDoesNot()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsString();
                               builder.WithKeys(ConfigurationKeys.ServiceName).AsString();
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithMessage("Sensitive configuration key 'DD_API_KEY' must be read with AsRedactedString, AsRedactedStringResult, AsRedactedDictionaryResult, or AsStringResult with compile-time recordValue: false");

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task SensitiveKeyInConsumerAssembly_ShouldReportDD0015()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsString();
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await AnalyzerTestHelper.VerifyAnalyzerInAssemblyWithSupportedConfigurationsAsync<ConfigurationBuilderWithKeysAnalyzer>(code, "Datadog.Trace.Tools.Runner", expected);
    }

    [Fact]
    public async Task SensitiveKeyWithAsDictionaryResult_ShouldReportDD0015()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsDictionaryResult();
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task SensitiveKeyWithRecordingAsStringResult_ShouldReportDD0015()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsStringResult(null, null, recordValue: true);
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task SensitiveKeyWithNonConstantRecordValue_ShouldReportDD0015()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               bool recordValue = false;
                               builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsStringResult(null, null, recordValue);
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task SensitiveKeyStoredWithoutAccessor_ShouldReportDD0015()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               var sensitive = builder.WithKeys({|#0:ConfigurationKeys.ApiKey|});
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task SensitiveKeyWithAsRedactedString_ShouldHaveNoDiagnostics()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
                           }
                       }
                   }
                   """;

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code);
    }

    [Fact]
    public async Task SensitiveKeyWithAsRedactedStringResult_ShouldHaveNoDiagnostics()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys(ConfigurationKeys.ApiKey).AsRedactedStringResult();
                           }
                       }
                   }
                   """;

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code);
    }

    [Fact]
    public async Task SensitiveKeyWithNonRecordingAsStringResult_ShouldHaveNoDiagnostics()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys(ConfigurationKeys.ApiKey).AsStringResult(null, null, recordValue: false);
                           }
                       }
                   }
                   """;

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code);
    }

    [Fact]
    public async Task SensitiveKeyWithParenthesesAndConversionAroundWithKeys_ShouldHaveNoDiagnostics()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               (builder.WithKeys(ConfigurationKeys.ApiKey)).AsRedactedString();
                               ((Telemetry.ConfigurationBuilder.HasKeys)builder.WithKeys(ConfigurationKeys.ApiKey)).AsRedactedString();
                           }
                       }
                   }
                   """;

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code);
    }

    [Fact]
    public async Task SensitiveKeyWithSameNamedExtensionAccessor_ShouldReportDD0015()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public static class MaliciousExtensions
                       {
                           public static string AsRedactedString(this Telemetry.ConfigurationBuilder.HasKeys keys, string ignored) => null;
                       }

                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsRedactedString("record-value");
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task SensitiveKeyWithMalformedSupportedConfigurations_ShouldReportDD0016()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys(ConfigurationKeys.ApiKey).AsString();
                           }
                       }
                   }
                   """;
        const string malformedYaml = """
                                     supportedConfigurations:
                                       DD_API_KEY:
                                         malformed property
                                     """;

        var expected = new DiagnosticResult(Dd0016, DiagnosticSeverity.Error).WithNoLocation();

        await AnalyzerTestHelper.VerifyDatadogAnalyzerWithSupportedConfigurationsAsync<ConfigurationBuilderWithKeysAnalyzer>(code, malformedYaml, expected);
    }

    [Theory]
    [InlineData("version: '2'")]
    [InlineData("supportedConfigurations:")]
    public async Task EmptySupportedConfigurations_ShouldReportDD0016(string yaml)
    {
        var expected = new DiagnosticResult(Dd0016, DiagnosticSeverity.Error).WithNoLocation();

        await AnalyzerTestHelper.VerifyDatadogAnalyzerWithSupportedConfigurationsAsync<ConfigurationBuilderWithKeysAnalyzer>(SensitiveConfigurationTypes, yaml, expected);
    }

    [Fact]
    public async Task SensitiveKeyWithMissingSupportedConfigurations_ShouldReportDD0016()
    {
        var code = SensitiveConfigurationTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               builder.WithKeys(ConfigurationKeys.ApiKey).AsString();
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0016, DiagnosticSeverity.Error).WithNoLocation();

        await AnalyzerTestHelper.VerifyDatadogAnalyzerWithoutSupportedConfigurationsAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public void CanceledSupportedConfigurationsRead_ShouldPropagateCancellation()
    {
        var options = new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(new CanceledAdditionalText()));
        var method = typeof(ConfigurationBuilderWithKeysAnalyzer).GetMethod("TryGetSensitiveKeys", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [options, CancellationToken.None, null]));
        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task AmbiguousSupportedConfigurations_ShouldReportDD0016()
    {
        var diagnostics = await CreateAnalyzer(
                              new LiteralAdditionalText("/first/supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml),
                              new LiteralAdditionalText("/second/supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml))
                             .GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diagnostics, x => x.Id == Dd0016);
    }

    [Fact]
    public async Task UnreadableSupportedConfigurations_ShouldReportDD0016()
    {
        var diagnostics = await CreateAnalyzer(new UnreadableAdditionalText()).GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diagnostics, x => x.Id == Dd0016);
    }

    [Fact]
    public async Task ValidWithKeysUsingConfigurationKeys_ShouldHaveNoDiagnostics()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationBuilderWithKeysAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    AnalyzerTestHelper.MinimalRequiredTypes,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration.Telemetry;

                    public struct ConfigurationBuilder
                    {
                        public HasKeys WithKeys(string key) => default;
                    }

                    public struct HasKeys
                    {
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public static partial class ConfigurationKeys
                    {
                        public const string TraceEnabled = "DD_TRACE_ENABLED";
                        public const string ServiceName = "DD_SERVICE";
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new Telemetry.ConfigurationBuilder();
                            builder.WithKeys(ConfigurationKeys.TraceEnabled);
                            builder.WithKeys(ConfigurationKeys.ServiceName);
                        }
                    }
                    """
                }
            }
        };

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml));
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ValidWithKeysUsingPlatformKeys_ShouldHaveNoDiagnostics()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationBuilderWithKeysAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    AnalyzerTestHelper.MinimalRequiredTypes,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration.Telemetry;

                    public struct ConfigurationBuilder
                    {
                        public HasKeys WithKeys(string key) => default;
                    }

                    public struct HasKeys
                    {
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public static partial class PlatformKeys
                    {
                        public const string CorProfilerPath = "CORECLR_PROFILER_PATH";
                        public const string AwsLambdaFunctionName = "AWS_LAMBDA_FUNCTION_NAME";
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new Telemetry.ConfigurationBuilder();
                            builder.WithKeys(PlatformKeys.CorProfilerPath);
                            builder.WithKeys(PlatformKeys.AwsLambdaFunctionName);
                        }
                    }
                    """
                }
            }
        };

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml));
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ValidWithKeysUsingNestedClasses_ShouldHaveNoDiagnostics()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationBuilderWithKeysAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    AnalyzerTestHelper.MinimalRequiredTypes,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration.Telemetry;

                    public struct ConfigurationBuilder
                    {
                        public HasKeys WithKeys(string key) => default;
                    }

                    public struct HasKeys
                    {
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public static partial class ConfigurationKeys
                    {
                        public static class CIVisibility
                        {
                            public const string Enabled = "DD_CIVISIBILITY_ENABLED";
                        }
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new Telemetry.ConfigurationBuilder();
                            builder.WithKeys(ConfigurationKeys.CIVisibility.Enabled);
                        }
                    }
                    """
                }
            }
        };

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml));
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }

    [Fact]
    public async Task WithKeysUsingHardcodedString_ShouldReportDD0007()
    {
        var code = AnalyzerTestHelper.MinimalRequiredTypes + """
                   namespace Datadog.Trace.Configuration.Telemetry
                   {
                       public struct ConfigurationBuilder { public HasKeys WithKeys(string key) => default; }
                       public struct HasKeys { }

                       public class TestClass
                       {
                           public void TestMethod()
                           {
                           var builder = new ConfigurationBuilder();
                           builder.WithKeys({|#0:"DD_TRACE_ENABLED"|});
                       }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0007, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithArguments("WithKeys", "DD_TRACE_ENABLED");

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task WithKeysUsingVariable_ShouldReportDD0008()
    {
        var code = AnalyzerTestHelper.MinimalRequiredTypes + """
                   namespace Datadog.Trace.Configuration.Telemetry
                   {
                       public struct ConfigurationBuilder { public HasKeys WithKeys(string key) => default; }
                       public struct HasKeys { }

                       public class TestClass
                       {
                           public void TestMethod()
                           {
                           var builder = new ConfigurationBuilder();
                           var myKey = "DD_TRACE_ENABLED";
                           builder.WithKeys({|#0:myKey|});
                       }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0008, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithArguments("WithKeys", "myKey");

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task WithKeysUsingMethodCall_ShouldReportDD0008()
    {
        var code = AnalyzerTestHelper.MinimalRequiredTypes + """
                   namespace Datadog.Trace.Configuration.Telemetry
                   {
                       public struct ConfigurationBuilder { public HasKeys WithKeys(string key) => default; }
                       public struct HasKeys { }

                       public class TestClass
                       {
                           public void TestMethod()
                           {
                           var builder = new ConfigurationBuilder();
                           builder.WithKeys({|#0:GetKey()|});
                       }

                       private string GetKey() => "DD_TRACE_ENABLED";
                   }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0008, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithArguments("WithKeys", "GetKey()");

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task WithKeysUsingStringInterpolation_ShouldReportDD0008()
    {
        var code = AnalyzerTestHelper.MinimalRequiredTypes + """
                   namespace Datadog.Trace.Configuration.Telemetry
                   {
                       public struct ConfigurationBuilder { public HasKeys WithKeys(string key) => default; }
                       public struct HasKeys { }

                       public class TestClass
                       {
                           public void TestMethod()
                           {
                           var builder = new ConfigurationBuilder();
                           var prefix = "DD_";
                           builder.WithKeys({|#0:$"{prefix}TRACE_ENABLED"|});
                       }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0008, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithArguments("WithKeys", "$\"{prefix}TRACE_ENABLED\"");

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    [Fact]
    public async Task WithKeysUsingConstantFromWrongClass_ShouldReportDD0008()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationBuilderWithKeysAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    AnalyzerTestHelper.MinimalRequiredTypes,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration.Telemetry;

                    public struct ConfigurationBuilder
                    {
                        public HasKeys WithKeys(string key) => default;
                    }

                    public struct HasKeys
                    {
                    }
                    """,
                    """
                    #nullable enable
                    namespace SomeOther.Namespace;

                    public static class MyKeys
                    {
                        public const string TraceEnabled = "DD_TRACE_ENABLED";
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration.Telemetry;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new ConfigurationBuilder();
                            builder.WithKeys({|#0:SomeOther.Namespace.MyKeys.TraceEnabled|});
                        }
                    }
                    """
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(Dd0008, DiagnosticSeverity.Error)
                        .WithLocation(0)
                        .WithArguments("WithKeys", "SomeOther.Namespace.MyKeys.TraceEnabled")
                }
            }
        };

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml));
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }

    [Fact]
    public async Task MultipleViolations_ShouldReportMultipleDiagnostics()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationBuilderWithKeysAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    AnalyzerTestHelper.MinimalRequiredTypes,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration.Telemetry;

                    public struct ConfigurationBuilder
                    {
                        public HasKeys WithKeys(string key) => default;
                    }

                    public struct HasKeys
                    {
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public static partial class ConfigurationKeys
                    {
                        public const string TraceEnabled = "DD_TRACE_ENABLED";
                    }
                    """,
                    """
                    #nullable enable
                    namespace Datadog.Trace.Configuration;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new Telemetry.ConfigurationBuilder();
                            var myKey = "DD_SERVICE";
                            
                            builder.WithKeys({|#0:"DD_ENV"|});
                            builder.WithKeys({|#1:myKey|});
                            builder.WithKeys(ConfigurationKeys.TraceEnabled);
                        }
                    }
                    """
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(Dd0007, DiagnosticSeverity.Error)
                        .WithLocation(0)
                        .WithArguments("WithKeys", "DD_ENV"),
                    new DiagnosticResult(Dd0008, DiagnosticSeverity.Error)
                        .WithLocation(1)
                        .WithArguments("WithKeys", "myKey")
                }
            }
        };

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", AnalyzerTestHelper.SupportedConfigurationsYaml));
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }

    [Fact]
    public async Task DifferentWithKeysMethodInDifferentNamespace_ShouldHaveNoDiagnostics()
    {
        var code = AnalyzerTestHelper.MinimalRequiredTypes + """
                   namespace Datadog.Trace.Configuration.Telemetry
                   {
                       public struct ConfigurationBuilder { public HasKeys WithKeys(string key) => default; }
                       public struct HasKeys { }
                   }
                   namespace SomeOther.Namespace
                   {
                       public struct ConfigurationBuilder
                       {
                           public HasKeys WithKeys(string key) => default;
                       }

                       public struct HasKeys
                       {
                       }

                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new ConfigurationBuilder();
                               builder.WithKeys("DD_TRACE_ENABLED");
                               builder.WithKeys("DD_SERVICE");
                           }
                       }
                   }
                   """;

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code);
    }

    [Fact]
    public async Task MissingRequiredType_ShouldReportDD0009()
    {
        // Only define ConfigurationBuilder, missing ConfigurationKeys and PlatformKeys
        var code = """
                   namespace Datadog.Trace.Configuration.Telemetry
                   {
                       public struct ConfigurationBuilder
                       {
                           public HasKeys WithKeys(string key) => default;
                       }

                       public struct HasKeys
                       {
                       }
                   }
                   """;

        var expected = new DiagnosticResult("DD0009", DiagnosticSeverity.Error)
                      .WithNoLocation()
                      .WithArguments("ConfigurationBuilderWithKeysAnalyzer", "Datadog.Trace.Configuration.ConfigurationKeys");

        await AnalyzerTestHelper.VerifyDatadogAnalyzerAsync<ConfigurationBuilderWithKeysAnalyzer>(code, expected);
    }

    private static CompilationWithAnalyzers CreateAnalyzer(params AdditionalText[] additionalFiles)
    {
        var compilation = CSharpCompilation.Create(
            "Datadog.Trace",
            [CSharpSyntaxTree.ParseText(SensitiveConfigurationTypes)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new AnalyzerOptions(additionalFiles.ToImmutableArray());
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ConfigurationBuilderWithKeysAnalyzer());
        return compilation.WithAnalyzers(analyzers, options);
    }

    private sealed class CanceledAdditionalText : AdditionalText
    {
        public override string Path => "supported-configurations.yaml";

        public override SourceText GetText(CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed class UnreadableAdditionalText : AdditionalText
    {
        public override string Path => "supported-configurations.yaml";

        public override SourceText GetText(CancellationToken cancellationToken = default) => null;
    }

    private sealed class LiteralAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(text);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
