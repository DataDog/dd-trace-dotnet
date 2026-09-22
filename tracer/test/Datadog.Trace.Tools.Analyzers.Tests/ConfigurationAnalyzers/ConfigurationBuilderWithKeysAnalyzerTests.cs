// <copyright file="ConfigurationBuilderWithKeysAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
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
    private const string SupportedConfigurationsYaml = """
                                                           version: '2'
                                                           supportedConfigurations:
                                                             DD_API_KEY:
                                                             - implementation: A
                                                               sensitive: true
                                                             DD_SERVICE:
                                                             - implementation: A
                                                               sensitive: false
                                                           """;

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
                               builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsString()|};
                               builder.WithKeys(ConfigurationKeys.ServiceName).AsString();
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithMessage("Sensitive configuration key 'DD_API_KEY' must be read with AsRedactedString, AsRedactedStringResult, AsRedactedDictionaryResult, or AsStringResult with compile-time recordValue: false");

        await VerifyAnalyzerAsync(code, expected);
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
                               builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsDictionaryResult()|};
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await VerifyAnalyzerAsync(code, expected);
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
                               builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsStringResult(null, null, recordValue: true)|};
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await VerifyAnalyzerAsync(code, expected);
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
                               builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsStringResult(null, null, recordValue)|};
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await VerifyAnalyzerAsync(code, expected);
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
                               var sensitive = {|#0:builder.WithKeys(ConfigurationKeys.ApiKey)|};
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await VerifyAnalyzerAsync(code, expected);
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

        await VerifyAnalyzerAsync(code);
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

        await VerifyAnalyzerAsync(code);
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

        await VerifyAnalyzerAsync(code);
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

        await VerifyAnalyzerAsync(code);
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
                               builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsRedactedString("record-value")|};
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);

        await VerifyAnalyzerAsync(code, expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("supportedConfigurations:\n  DD_API_KEY:\n    malformed property")]
    public async Task ExistingDiagnosticsStillRunWithoutValidSupportedConfigurations(string supportedConfigurationsYaml)
    {
        var code = AnalyzerTestHelper.RequiredTypes + """
                   namespace Datadog.Trace.Configuration
                   {
                       public class TestClass
                       {
                           public void TestMethod()
                           {
                               var builder = new Telemetry.ConfigurationBuilder();
                               var key = "DD_SERVICE";
                               builder.WithKeys({|#0:"DD_API_KEY"|});
                               builder.WithKeys({|#1:key|});
                           }
                       }
                   }
                   """;

        var expected = new DiagnosticResult(Dd0007, DiagnosticSeverity.Error)
                      .WithLocation(0)
                      .WithArguments("WithKeys", "DD_API_KEY");
        var variableExpected = new DiagnosticResult(Dd0008, DiagnosticSeverity.Error)
                              .WithLocation(1)
                              .WithArguments("WithKeys", "key");

        await VerifyAnalyzerAsync(code, supportedConfigurationsYaml, expected, variableExpected);
    }

    [Fact]
    public async Task SensitiveKeyCacheUpdatesWhenSupportedConfigurationsChange()
    {
        var diagnosticCode = SensitiveConfigurationTypes + """
                             namespace Datadog.Trace.Configuration
                             {
                                 public class TestClass
                                 {
                                     public void TestMethod()
                                     {
                                         var builder = new Telemetry.ConfigurationBuilder();
                                         builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsString()|};
                                     }
                                 }
                             }
                             """;
        var expected = new DiagnosticResult(Dd0015, DiagnosticSeverity.Error).WithLocation(0);
        await VerifyAnalyzerAsync(diagnosticCode, expected);

        const string changedYaml = """
                                   version: '2'
                                   supportedConfigurations:
                                     DD_API_KEY:
                                     - implementation: A
                                       sensitive: false
                                     DD_SERVICE:
                                     - implementation: A
                                       sensitive: true
                                   """;
        var noDiagnosticCode = SensitiveConfigurationTypes + """
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

        await VerifyAnalyzerAsync(noDiagnosticCode, changedYaml);
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

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", SupportedConfigurationsYaml));
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

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", SupportedConfigurationsYaml));
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

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", SupportedConfigurationsYaml));
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

        await VerifyAnalyzerAsync(code, expected);
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

        await VerifyAnalyzerAsync(code, expected);
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

        await VerifyAnalyzerAsync(code, expected);
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

        await VerifyAnalyzerAsync(code, expected);
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

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", SupportedConfigurationsYaml));
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

        test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", SupportedConfigurationsYaml));
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

        await VerifyAnalyzerAsync(code);
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

        await VerifyAnalyzerAsync(code, expected);
    }

    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => VerifyAnalyzerAsync(source, SupportedConfigurationsYaml, expected);

    private static async Task VerifyAnalyzerAsync(string source, string supportedConfigurationsYaml, params DiagnosticResult[] expected)
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationBuilderWithKeysAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source }
            }
        };

        if (supportedConfigurationsYaml is not null)
        {
            test.TestState.AdditionalFiles.Add(("supported-configurations.yaml", supportedConfigurationsYaml));
        }

        test.TestState.ExpectedDiagnostics.AddRange(expected);
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }
}
