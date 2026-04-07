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
}
