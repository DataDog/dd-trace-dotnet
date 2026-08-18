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
                    public string AsString(string defaultValue) => null;
                    public string AsString(System.Func<string, bool> validator) => null;
                    public string AsRedactedString() => null;
                    public string AsRedactedString(string defaultValue) => null;
                    public object AsDictionaryResult() => null;
                    public object AsDictionaryResult(bool allowOptionalMappings) => null;
                    public object AsDictionaryResult(char separator) => null;
                    public object AsDictionaryResult(bool allowOptionalMappings, char separator) => null;
                    public object AsDictionaryResult(object parser) => null;
                    public object AsRedactedDictionaryResult(char separator) => null;
                    public object AsStringResult() => null;
                    public object AsStringResult(object converter) => null;
                    public object AsStringResult(object validator, object converter) => null;
                    public object AsStringResult(object validator, object converter, bool recordValue) => null;
                    public object AsRedactedStringResult() => null;
                    public object AsRedactedStringResult(object converter) => null;
                    public object AsRedactedStringResult(object validator, object converter) => null;
                }
            }
        }

        namespace Datadog.Trace.Configuration
        {
            public static class HasKeysExtensions
            {
                public static object AsStringResult(this Telemetry.ConfigurationBuilder.HasKeys hasKeys, int one, int two, int three, int four) => null;
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

    [Theory]
    [InlineData("AsString()", "AsRedactedString()")]
    [InlineData("AsString(\"default\")", "AsRedactedString(\"default\")")]
    [InlineData("AsStringResult()", "AsRedactedStringResult()")]
    [InlineData("AsStringResult(converter: null)", "AsRedactedStringResult(converter: null)")]
    [InlineData("AsStringResult(validator: null, converter: null)", "AsRedactedStringResult(validator: null, converter: null)")]
    [InlineData("AsStringResult(null, null, recordValue: true)", "AsRedactedStringResult(null, null)")]
    [InlineData("AsStringResult(recordValue: true, converter: null, validator: null)", "AsRedactedStringResult(converter: null, validator: null)")]
    [InlineData("AsDictionaryResult()", "AsRedactedDictionaryResult(separator: ':')")]
    [InlineData("AsDictionaryResult(';')", "AsRedactedDictionaryResult(';')")]
    [InlineData("AsDictionaryResult(allowOptionalMappings: false)", "AsRedactedDictionaryResult(separator: ':')")]
    [InlineData("AsDictionaryResult(allowOptionalMappings: false, separator: ';')", "AsRedactedDictionaryResult(separator: ';')")]
    public async Task UnsafeAccessorIsReplacedWithRedactedAccessor(string accessor, string redactedAccessor)
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
            },
            FixedState =
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
                              _ = builder.WithKeys(ConfigurationKeys.ApiKey).{{redactedAccessor}};
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
    [InlineData("AsString(value => true)")]
    [InlineData("AsDictionaryResult(allowOptionalMappings: true)")]
    [InlineData("AsDictionaryResult(new object())")]
    [InlineData("AsStringResult(null, null, bool.Parse(\"true\"))")]
    [InlineData("AsStringResult(1, 2, 3, 4)")]
    public async Task OtherUnsafeAccessorsDoNotOfferCodeFix(string accessor)
    {
        var source = $$"""
          namespace Datadog.Trace.Configuration;

          public class TestClass
          {
              public void TestMethod()
              {
                  var builder = new Telemetry.ConfigurationBuilder();
                  _ = builder.WithKeys(ConfigurationKeys.ApiKey).{|#0:{{accessor}}|};
              }
          }
          """;
        var test = new Test
        {
            NumberOfIncrementalIterations = 0,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            TestState =
            {
                Sources =
                {
                    ConfigurationTypes,
                    source
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
                    source
                }
            }
        };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));

        await test.RunAsync();
    }
}
