// <copyright file="PlatformKeyAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.PlatformKeyAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers
{
    public class PlatformKeyAnalyzerTests
    {
        private const string IConfigKeyInterface = """
            namespace Datadog.Trace.Configuration.ConfigurationSources.Registry
            {
                public interface IConfigKey
                {
                    string GetKey();
                }
            }
            """;

        [Theory]
        [InlineData("WEBSITE_SITE_NAME")] // Platform key
        [InlineData("AWS_LAMBDA_FUNCTION_NAME")] // Platform key
        [InlineData("CUSTOM_KEY")] // Non-reserved prefix
        public async Task ValidPlatformKeys_NoError(string keyName)
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.ConfigurationSources.Registry
                {
                    internal readonly struct PlatformKeyCustom : IConfigKey
                    {
                        public string GetKey() => "{{keyName}}";
                    }
                }
                """;

            await Verifier.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task GeneratedConfigKey_InGeneratedNamespace_NoError()
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.ConfigurationSources.Registry.Generated
                {
                    internal readonly struct ConfigKeyDdEnv : IConfigKey
                    {
                        public string GetKey() => "DD_ENV";
                    }
                }
                """;

            await Verifier.VerifyAnalyzerAsync(source);
        }

        [Theory]
        [InlineData("DD_", "DD_CUSTOM_KEY")]
        [InlineData("_DD_", "_DD_CUSTOM_KEY")]
        [InlineData("DATADOG_", "DATADOG_CUSTOM_KEY")]
        [InlineData("OTEL_", "OTEL_CUSTOM_KEY")]
        public async Task ManualConfigKey_WithReservedPrefix_ReportsDD0009(string prefix, string keyName)
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.ConfigurationSources.Registry
                {
                    internal readonly struct ConfigKeyCustom : IConfigKey
                    {
                        public string GetKey() => {|#0:"{{keyName}}"|};
                    }
                }
                """;

            var expected = Verifier.Diagnostic("DD0009")
                .WithLocation(0)
                .WithArguments("ConfigKeyCustom", keyName, prefix);

            await Verifier.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task ConfigKey_WithMethodCallReturn_ReportsDD0010()
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.ConfigurationSources.Registry
                {
                    internal readonly struct ConfigKeyCustom : IConfigKey
                    {
                        private static string GetKeyValue() => "CUSTOM_KEY";
                        
                        public string GetKey() => {|#0:GetKeyValue()|};
                    }
                }
                """;

            var expected = Verifier.Diagnostic("DD0010")
                .WithLocation(0)
                .WithArguments("ConfigKeyCustom");

            await Verifier.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task NonIConfigKeyStruct_NoError()
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.ConfigurationSources.Registry
                {
                    internal readonly struct SomeOtherStruct
                    {
                        public string GetKey() => "DD_CUSTOM_KEY";
                    }
                }
                """;

            await Verifier.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task IntegrationKeyTypes_AreExcluded_NoError()
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.ConfigurationSources.Registry
                {
                    internal readonly struct IntegrationNameConfigKey : IConfigKey
                    {
                        private readonly string _integrationName;

                        public IntegrationNameConfigKey(string integrationName)
                        {
                            _integrationName = integrationName;
                        }

                        public string GetKey() => string.Format("DD_TRACE_{0}_ENABLED", _integrationName);
                    }

                    internal readonly struct IntegrationAnalyticsEnabledConfigKey : IConfigKey
                    {
                        private readonly string _integrationName;

                        public IntegrationAnalyticsEnabledConfigKey(string integrationName)
                        {
                            _integrationName = integrationName;
                        }

                        public string GetKey() => string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", _integrationName);
                    }
                }
                """;

            await Verifier.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ConfigKeyAlias_NestedInConfigurationBuilder_IsExcluded_NoError()
        {
            var source = $$"""
                {{IConfigKeyInterface}}

                namespace Datadog.Trace.Configuration.Telemetry
                {
                    using Datadog.Trace.Configuration.ConfigurationSources.Registry;

                    internal class ConfigurationBuilder
                    {
                        internal readonly struct ConfigKeyAlias : IConfigKey
                        {
                            private readonly string _alias;

                            public ConfigKeyAlias(string alias)
                            {
                                _alias = alias;
                            }

                            public string GetKey() => _alias;
                        }
                    }
                }
                """;

            await Verifier.VerifyAnalyzerAsync(source);
        }
    }
}
