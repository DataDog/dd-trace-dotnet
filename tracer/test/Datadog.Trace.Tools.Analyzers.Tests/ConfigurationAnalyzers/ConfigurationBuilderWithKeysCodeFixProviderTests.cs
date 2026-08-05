// <copyright file="ConfigurationBuilderWithKeysCodeFixProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Test = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.ConfigurationBuilderWithKeysAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.ConfigurationBuilderWithKeysCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

public class ConfigurationBuilderWithKeysCodeFixProviderTests
{
    private const string ConfigurationTypes = """
        namespace Datadog.Trace.Configuration
        {
            public static partial class ConfigurationKeys
            {
                public const string ApiKey = "DD_API_KEY";
            }

            public static partial class PlatformKeys { }
        }

        namespace Datadog.Trace.Configuration.Telemetry
        {
            public struct ConfigurationBuilder
            {
                public HasKeys WithKeys(string key) => default;

                public struct HasKeys
                {
                    public string AsString() => null;
                    public string AsString(object validator) => null;
                    public string AsRedactedString() => null;
                    public object AsDictionaryResult() => null;
                    public object AsStringResult(object validator, object converter, bool recordValue) => null;
                }
            }
        }
        """;

    private const string SupportedConfigurationsYaml = """
        version: '2'
        supportedConfigurations:
          DD_API_KEY:
          - implementation: A
            sensitive: true
        """;

    [Fact]
    public async Task AsStringIsReplacedWithAsRedactedString()
    {
        var test = new Test
        {
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            TestState =
            {
                Sources =
                {
                    ConfigurationTypes,
                    """
                    namespace Datadog.Trace.Configuration;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new Telemetry.ConfigurationBuilder();
                            _ = builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:AsString()|};
                        }
                    }
                    """
                },
                AdditionalFiles =
                {
                    ("supported-configurations.yaml", SupportedConfigurationsYaml)
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("DD0015", DiagnosticSeverity.Error).WithLocation(0)
                }
            },
            FixedState =
            {
                Sources =
                {
                    ConfigurationTypes,
                    """
                    namespace Datadog.Trace.Configuration;

                    public class TestClass
                    {
                        public void TestMethod()
                        {
                            var builder = new Telemetry.ConfigurationBuilder();
                            _ = builder.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
                        }
                    }
                    """
                }
            }
        };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));

        await test.RunAsync();
    }

    [Theory]
    [InlineData("AsString(null)")]
    [InlineData("AsDictionaryResult()")]
    [InlineData("AsStringResult(null, null, recordValue: true)")]
    public async Task OtherUnsafeAccessorsDoNotOfferCodeFix(string accessor)
    {
        var test = new Test
        {
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            TestState =
            {
                Sources =
                {
                    ConfigurationTypes,
                    $$"""
                      namespace Datadog.Trace.Configuration;

                      public class TestClass
                      {
                          public void TestMethod()
                          {
                              var builder = new Telemetry.ConfigurationBuilder();
                              _ = builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:{{accessor}}|};
                          }
                      }
                      """
                },
                AdditionalFiles =
                {
                    ("supported-configurations.yaml", SupportedConfigurationsYaml)
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("DD0015", DiagnosticSeverity.Error).WithLocation(0)
                }
            }
        };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));

        await test.RunAsync();
    }
}
