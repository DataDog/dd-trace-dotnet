// <copyright file="TracerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class TracerSettingsTests : SettingsTestsBase
    {
        private readonly Mock<IAgentWriter> _writerMock;
        private readonly Mock<ITraceSampler> _samplerMock;

        public TracerSettingsTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            _samplerMock = new Mock<ITraceSampler>();
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env, null)]
        [InlineData(ConfigurationKeys.Environment, Tags.Env, "custom-env")]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version, null)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version, "custom-version")]
        public void ConfiguredTracerSettings_DefaultTagsSetFromEnvironmentVariable(string environmentVariableKey, string tagKey, string value)
        {
            var collection = new NameValueCollection { { environmentVariableKey, value } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(tagKey), value);
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version)]
        public void DDVarTakesPrecedenceOverDDTags(string envKey, string tagKey)
        {
            string envValue = $"ddenv-custom-{tagKey}";
            string tagsLine = $"{tagKey}:ddtags-custom-{tagKey}";
            var collection = new NameValueCollection { { envKey, envValue }, { ConfigurationKeys.GlobalTags, tagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            Assert.True(settings.GlobalTags.Any());

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(tagKey), envValue);
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("1", true)]
        [InlineData("0", false)]
        public void TraceEnabled(string value, bool areTracesEnabled)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.TraceEnabled, value }
            };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings));

            Assert.Equal(areTracesEnabled, tracerSettings.TraceEnabled);

            _writerMock.Invocations.Clear();

            var tracer = new Tracer(tracerSettings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("TestTracerDisabled");
            span.Dispose();

            var assertion = areTracesEnabled ? Times.Once() : Times.Never();

            _writerMock.Verify(w => w.WriteTrace(It.IsAny<ArraySegment<Span>>()), assertion);
        }

        [Theory]
        [InlineData("http://localhost:7777/agent?querystring", "http://127.0.0.1:7777/agent?querystring")]
        [InlineData("http://datadog:7777/agent?querystring", "http://datadog:7777/agent?querystring")]
        public void ReplaceLocalhost(string original, string expected)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.AgentUri, original }
            };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings));

            Assert.Equal(expected, tracerSettings.Exporter.AgentUri.ToString());
        }

        [Theory]
        [InlineData("a,b,c,d,,f", new[] { "a", "b", "c", "d", "f" })]
        [InlineData(" a, b ,c, ,,f ", new[] { "a", "b", "c", "f" })]
        [InlineData("a,b, c ,d,      e      ,f  ", new[] { "a", "b", "c", "d", "e", "f" })]
        [InlineData("a,b,c,d,e,f", new[] { "a", "b", "c", "d", "e", "f" })]
        [InlineData("", new string[0])]
        public void ParseStringArraySplit(string input, string[] expected)
        {
            var separators = new[] { ',' };
            var result = TracerSettings.TrimSplitString(input, separators);

            result.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData("404 -401, 419,344_ 23-302, 201,_5633-55, 409-411", "401,402,403,404,419,201,409,410,411")]
        [InlineData("-33, 500-503,113#53,500-502-200,456_2, 590-590", "500,501,502,503,590")]
        [InlineData("800", "")]
        [InlineData("599-605,700-800", "599")]
        [InlineData("400-403, 500-501-234, s342, 500-503", "400,401,402,403,500,501,502,503")]
        public void ParseHttpCodes(string original, string expected)
        {
            bool[] errorStatusCodesArray = TracerSettings.ParseHttpCodesToArray(original);
            string[] expectedKeysArray = expected.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var value in expectedKeysArray)
            {
                Assert.True(errorStatusCodesArray[int.Parse(value)]);
            }
        }

        [Fact]
        public void SetServiceNameMappings_AddsMappings()
        {
            var collection = new NameValueCollection { };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            settings.ServiceNameMappings.Should().BeNullOrEmpty();

            var mappings = new Dictionary<string, string> { { "elasticsearch", "custom-name" } };
            settings.SetServiceNameMappings(mappings);
            settings.ServiceNameMappings.Should().BeEquivalentTo(mappings);
        }

        [Fact]
        public void SetServiceNameMappings_ReplacesExistingMappings()
        {
            var collection = new NameValueCollection { };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            settings.ServiceNameMappings.Should().BeNullOrEmpty();

            var mappings = new Dictionary<string, string> { { "elasticsearch", "custom-name" } };
            settings.SetServiceNameMappings(mappings);
            settings.ServiceNameMappings.Should().BeEquivalentTo(mappings);

            var newMappings = new Dictionary<string, string> { { "sql-server", "custom-db" } };
            settings.SetServiceNameMappings(newMappings);
            settings.ServiceNameMappings.Should().BeEquivalentTo(newMappings);
        }

        [Fact]
        public void Constructor_HandlesNullSource()
        {
            var tracerSettings = new TracerSettings(null);
            tracerSettings.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_HandlesEmptyource()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()));
            tracerSettings.Should().NotBeNull();
        }

        [Fact]
        public void SetClientHttpCodes()
        {
            SetAndValidateStatusCodes((s, c) => s.SetHttpClientErrorStatusCodes(c), s => s.HttpClientErrorStatusCodes);
        }

        [Fact]
        public void SetServerHttpCodes()
        {
            SetAndValidateStatusCodes((s, c) => s.SetHttpServerErrorStatusCodes(c), s => s.HttpServerErrorStatusCodes);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void Environment(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Environment, value));
            var settings = new TracerSettings(source);

            settings.Environment.Should().Be(expected);
        }

        [Theory]
        [InlineData("test", null, "test")]
        [InlineData("test", "error", "test")]
        [InlineData(null, "test", "test")]
        [InlineData("", "test", "")]
        [InlineData(null, null, null)]
        public void ServiceName(string value, string legacyValue, string expected)
        {
            const string legacyServiceName = "DD_SERVICE_NAME";

            var source = CreateConfigurationSource((ConfigurationKeys.ServiceName, value), (legacyServiceName, legacyValue));
            var settings = new TracerSettings(source);

            settings.ServiceName.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ServiceVersion(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ServiceVersion, value));
            var settings = new TracerSettings(source);

            settings.ServiceVersion.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void GitCommitSha(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.GitCommitSha, value));
            var settings = new TracerSettings(source);

            settings.GitCommitSha.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void GitRepositoryUrl(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.GitRepositoryUrl, value));
            var settings = new TracerSettings(source);

            settings.GitRepositoryUrl.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void GitMetadataEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.GitMetadataEnabled, value));
            var settings = new TracerSettings(source);

            settings.GitMetadataEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, true, new string[0])]
        [InlineData(null, false, new[] { "OpenTelemetry" })]
        [InlineData("", true, new string[0])]
        [InlineData("test", false, new[] { "test", "OpenTelemetry" })]
        [InlineData("test1;TEST1;test2", true, new[] { "test1", "test2" })]
        public void DisabledIntegrationNames(string value, bool isOpenTelemetryEnabled, string[] expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DisabledIntegrations, value),
                (ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, isOpenTelemetryEnabled ? "1" : "0"));
            var settings = new TracerSettings(source);

            settings.DisabledIntegrationNames.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void AnalyticsEnabled(string value, bool expected)
        {
#pragma warning disable 618 // App analytics is deprecated, but still used
            var source = CreateConfigurationSource((ConfigurationKeys.GlobalAnalyticsEnabled, value));
            var settings = new TracerSettings(source);

            settings.AnalyticsEnabled.Should().Be(expected);
#pragma warning restore 618
        }

        [Fact]
        public void Integrations()
        {
            // Further testing is done in IntegrationSettingsTests
            var source = CreateConfigurationSource(
                ($"DD_{IntegrationRegistry.Names[1]}_ENABLED", "0"),
                ($"DD_{IntegrationRegistry.Names[2]}_ENABLED", "1"));

            var settings = new TracerSettings(source);

            settings.Integrations[IntegrationRegistry.Names[0]].Enabled.Should().BeNull();
            settings.Integrations[IntegrationRegistry.Names[1]].Enabled.Should().BeFalse();
            settings.Integrations[IntegrationRegistry.Names[2]].Enabled.Should().BeTrue();
        }

        [Theory]
        [InlineData("10", null, 10)]
        [InlineData("10", "50", 10)]
        [InlineData(null, "10", 10)]
        [InlineData("", "10", 10)]
        [InlineData("", "", 100)]
        [InlineData("A", "A", 100)]
        public void MaxTracesSubmittedPerSecond(string value, string legacyValue, int expected)
        {
#pragma warning disable 618 // this parameter has been replaced but may still be used
            var source = CreateConfigurationSource((ConfigurationKeys.TraceRateLimit, value), (ConfigurationKeys.MaxTracesSubmittedPerSecond, legacyValue));
            var settings = new TracerSettings(source);

            settings.MaxTracesSubmittedPerSecond.Should().Be(expected);
#pragma warning restore 618
        }

        [Theory]
        [InlineData("key1:value1,key2:value2", "key3:value3", new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,invalid,key2: value2", "key3:value3", new[] { "key1:value1", "key2:value2" })]
        [InlineData("invalid", "key1:value1,key2:value2", new string[0])]
        [InlineData(null, "key1:value1,key2:value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("", "key1:value1,key2:value2", new string[0])]
        [InlineData("", "", new string[0])]
        [InlineData("invalid", "invalid", new string[0])]
        public void GlobalTags(string value, string legacyValue, string[] expected)
        {
            const string legacyGlobalTagsKey = "DD_TRACE_GLOBAL_TAGS";

            var source = CreateConfigurationSource((ConfigurationKeys.GlobalTags, value), (legacyGlobalTagsKey, legacyValue));
            var settings = new TracerSettings(source);

            settings.GlobalTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [InlineData("key1:value1,key2:value2", true, new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,empty,key2: value2", true, new[] { "key1:value1", "empty:", "key2:value2" })]
        [InlineData(null, true, new string[0])]
        [InlineData("", true, new string[0])]
        [InlineData("key1 :val.ue1?", false, new[] { "key1:val_ue1_" })]
        [InlineData("key1 :val.ue1?", true, new[] { "key1:val.ue1_" })]
        public void HeaderTags(string value, bool normalizationFixEnabled, string[] expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.HeaderTags, value),
                (ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled, normalizationFixEnabled ? "1" : "0"));
            var settings = new TracerSettings(source);

            settings.HeaderTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [InlineData("v1", SchemaVersion.V1)]
        [InlineData("V1", SchemaVersion.V1)]
        [InlineData("", SchemaVersion.V0)]
        [InlineData(null, SchemaVersion.V0)]
        [InlineData("v1 ", SchemaVersion.V0)]
        public void MetadataSchemaVersion(string value, object expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.MetadataSchemaVersion, value));
            var settings = new TracerSettings(source);

            settings.MetadataSchemaVersion.Should().Be((SchemaVersion)expected);
        }

        [Theory]
        [InlineData("key1:value1,key2:value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,invalid,key2: value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("invalid", new string[0])]
        [InlineData(null, null)]
        [InlineData("", new string[0])]
        public void ServiceNameMappings(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ServiceNameMappings, value));
            var settings = new TracerSettings(source);

            settings.ServiceNameMappings.Should().BeEquivalentTo(expected?.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [InlineData("key1:value1,key2:value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,invalid,key2: value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("invalid", new string[0])]
        [InlineData(null, null)]
        [InlineData("", new string[0])]
        public void PeerServiceNameMappings(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.PeerServiceNameMappings, value));
            var settings = new TracerSettings(source);

            settings.PeerServiceNameMappings.Should().BeEquivalentTo(expected?.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void TracerMetricsEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.TracerMetricsEnabled, value));
            var settings = new TracerSettings(source);

            settings.TracerMetricsEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void StatsComputationEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.StatsComputationEnabled, value));
            var settings = new TracerSettings(source);

            settings.StatsComputationEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32TestCases), 10)]
        public void StatsComputationInterval(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.StatsComputationInterval, value));
            var settings = new TracerSettings(source);

            settings.StatsComputationInterval.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void RuntimeMetricsEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.RuntimeMetricsEnabled, value));
            var settings = new TracerSettings(source);

            settings.RuntimeMetricsEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CustomSamplingRules(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CustomSamplingRules, value));
            var settings = new TracerSettings(source);

            settings.CustomSamplingRules.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void SpanSamplingRules(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.SpanSamplingRules, value));
            var settings = new TracerSettings(source);

            settings.SpanSamplingRules.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(DoubleTestCases))]
        public void GlobalSamplingRate(string value, double? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.GlobalSamplingRate, value));
            var settings = new TracerSettings(source);

            settings.GlobalSamplingRate.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void StartupDiagnosticLogEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.StartupDiagnosticLogEnabled, value));
            var settings = new TracerSettings(source);

            settings.StartupDiagnosticLogEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32TestCases), 1024 * 1024 * 10)]
        public void TraceBufferSize(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.BufferSize, value));
            var settings = new TracerSettings(source);

            settings.TraceBufferSize.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32TestCases), 100)]
        public void TraceBatchInterval(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.SerializationBatchInterval, value));
            var settings = new TracerSettings(source);

            settings.TraceBatchInterval.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void RouteTemplateResourceNamesEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, value));
            var settings = new TracerSettings(source);

            settings.RouteTemplateResourceNamesEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("1", "1", true)]
        [InlineData("true", "1", true)]
        [InlineData("0", "1", false)]
        [InlineData("false", "1", false)]
        [InlineData("invalid", "1", false)]
        [InlineData("invalid", "0", true)]
        [InlineData("", "0", true)]
        [InlineData(null, "0", true)]
        [InlineData(null, null, false)]
        public void ExpandRouteTemplatesEnabled(string value, string fallbackValue, bool expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.ExpandRouteTemplatesEnabled, value),
                (ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, fallbackValue));
            var settings = new TracerSettings(source);

            settings.ExpandRouteTemplatesEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void KafkaCreateConsumerScopeEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.KafkaCreateConsumerScopeEnabled, value));
            var settings = new TracerSettings(source);

            settings.KafkaCreateConsumerScopeEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void DelayWcfInstrumentationEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.DelayWcfInstrumentationEnabled, value));
            var settings = new TracerSettings(source);

            settings.DelayWcfInstrumentationEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void WcfObfuscationEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled, value));
            var settings = new TracerSettings(source);

            settings.WcfObfuscationEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), TracerSettingsConstants.DefaultObfuscationQueryStringRegex, Strings.AllowEmpty)]
        public void ObfuscationQueryStringRegex(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ObfuscationQueryStringRegex, value));
            var settings = new TracerSettings(source);

            settings.ObfuscationQueryStringRegex.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void QueryStringReportingEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.QueryStringReportingEnabled, value));
            var settings = new TracerSettings(source);

            settings.QueryStringReportingEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32TestCases), 5000)]
        public void QueryStringReportingSize(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.QueryStringReportingSize, value));
            var settings = new TracerSettings(source);

            settings.QueryStringReportingSize.Should().Be(expected);
        }

        [Theory]
        [InlineData("1.5", 1.5d)]
        [InlineData("-1", 200)]
        [InlineData("", 200)]
        [InlineData(null, 200)]
        [InlineData("invalid", 200)]
        public void ObfuscationQueryStringRegexTimeout(string value, double expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ObfuscationQueryStringRegexTimeout, value));
            var settings = new TracerSettings(source);

            settings.ObfuscationQueryStringRegexTimeout.Should().Be(expected);
        }

        [Theory]
        [InlineData("1", "0", true)]
        [InlineData("0", "0", false)]
        [InlineData("true", "1", true)]
        [InlineData("false", "1", false)]
        [InlineData(null, "1", true)]
        [InlineData(null, "true", true)]
        [InlineData(null, "0", false)]
        [InlineData(null, "false", false)]
        [InlineData(null, null, false)]
        [InlineData("", "", false)]
        public void IsActivityListenerEnabled(string value, string fallbackValue, bool expected)
        {
            const string fallbackKey = "DD_TRACE_ACTIVITY_LISTENER_ENABLED";

            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, value), (fallbackKey, fallbackValue));
            var settings = new TracerSettings(source);

            settings.IsActivityListenerEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("test1,, ,test2", "test3,, ,test4", "test5,, ,test6", new[] { "test1", "test2" })]
        [InlineData("", "test3,, ,test4", "test5,, ,test6", new[] { "tracecontext", "Datadog" })]
        [InlineData(null, "test3,, ,test4", "test5,, ,test6", new[] { "test3", "test4" })]
        [InlineData(null, null, "test5,, ,test6", new[] { "test5", "test6" })]
        [InlineData(null, null, null, new[] { "tracecontext", "Datadog" })]
        public void PropagationStyleInject(string value, string legacyValue, string fallbackValue, string[] expected)
        {
            const string legacyKey = "DD_PROPAGATION_STYLE_INJECT";

            foreach (var isActivityListenerEnabled in new[] { true, false })
            {
                var source = CreateConfigurationSource(
                    (ConfigurationKeys.PropagationStyleInject, value),
                    (legacyKey, legacyValue),
                    (ConfigurationKeys.PropagationStyle, fallbackValue),
                    (ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, isActivityListenerEnabled ? "1" : "0"));

                var settings = new TracerSettings(source);

                settings.PropagationStyleInject.Should().BeEquivalentTo(expected);
            }
        }

        [Theory]
        [InlineData("test1,, ,test2", "test3,, ,test4", "test5,, ,test6", new[] { "test1", "test2" })]
        [InlineData("", "test3,, ,test4", "test5,, ,test6", new[] { "tracecontext", "Datadog" })]
        [InlineData(null, "test3,, ,test4", "test5,, ,test6", new[] { "test3", "test4" })]
        [InlineData(null, null, "test5,, ,test6", new[] { "test5", "test6" })]
        [InlineData(null, null, null, new[] { "tracecontext", "Datadog" })]
        public void PropagationStyleExtract(string value, string legacyValue, string fallbackValue, string[] expected)
        {
            const string legacyKey = "DD_PROPAGATION_STYLE_EXTRACT";

            foreach (var isActivityListenerEnabled in new[] { true, false })
            {
                var source = CreateConfigurationSource(
                    (ConfigurationKeys.PropagationStyleExtract, value),
                    (legacyKey, legacyValue),
                    (ConfigurationKeys.PropagationStyle, fallbackValue),
                    (ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, isActivityListenerEnabled ? "1" : "0"));

                var settings = new TracerSettings(source);

                settings.PropagationStyleExtract.Should().BeEquivalentTo(expected);
            }
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "", Strings.AllowEmpty)]
        public void TraceMethods(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.TraceMethods, value));
            var settings = new TracerSettings(source);

            settings.TraceMethods.Should().Be(expected);
        }

        [Theory]
        [InlineData("key1:value1,key2:value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,empty,key2: value2", new[] { "key1:value1", "empty:", "key2:value2" })]
        [InlineData(null, new string[0])]
        [InlineData("", new string[0])]
        [InlineData("key1 :val.ue1?", new[] { "key1:val.ue1_" })]
        public void GrpcTags(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.GrpcTags, value));
            var settings = new TracerSettings(source);

            settings.GrpcTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [InlineData("", TagPropagation.OutgoingTagPropagationHeaderMaxLength)]
        [InlineData(null, TagPropagation.OutgoingTagPropagationHeaderMaxLength)]
        [InlineData("invalid", TagPropagation.OutgoingTagPropagationHeaderMaxLength)]
        [InlineData("512", 512)]
        [InlineData("513", TagPropagation.OutgoingTagPropagationHeaderMaxLength)]
        [InlineData("0", 0)]
        [InlineData("-1", TagPropagation.OutgoingTagPropagationHeaderMaxLength)]
        public void OutgoingTagPropagationHeaderMaxLength(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.TagPropagation.HeaderMaxLength, value));
            var settings = new TracerSettings(source);

            settings.OutgoingTagPropagationHeaderMaxLength.Should().Be(expected);
        }

        [Theory]
        [InlineData("test1", "test2", "test1")]
        [InlineData(null, "test2", "test2")]
        [InlineData("", "test2", "")]
        [InlineData(null, null, null)]
        public void IpHeader(string value, string fallbackValue, string expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.IpHeader, value),
                (ConfigurationKeys.AppSec.CustomIpHeader, fallbackValue));
            var settings = new TracerSettings(source);

            settings.IpHeader.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void IpHeaderEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.IpHeaderEnabled, value));
            var settings = new TracerSettings(source);

            settings.IpHeaderEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void IsDataStreamsMonitoringEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DataStreamsMonitoring.Enabled, value));
            var settings = new TracerSettings(source);

            settings.IsDataStreamsMonitoringEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void IsRareSamplerEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.RareSamplerEnabled, value));
            var settings = new TracerSettings(source);

            settings.IsRareSamplerEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void IsRunningInAzureAppService(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, value));
            var settings = new TracerSettings(source);

            settings.IsRunningInAzureAppService.Should().Be(expected);
        }

        [Fact]
        public void DisableTracerIfNoApiKeyInAas()
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1"));
            var settings = new TracerSettings(source);

            settings.TraceEnabled.Should().BeFalse();
        }

        // The HttpClientExcludedUrlSubstrings tests rely on Lambda.Create() which uses environment variables
        // See TracerSettingsServerlessTests for tests which rely on environment variables

        [Theory]
        [InlineData("", DbmPropagationLevel.Disabled)]
        [InlineData(null, DbmPropagationLevel.Disabled)]
        [InlineData("invalid", DbmPropagationLevel.Disabled)]
        [InlineData("Disabled", DbmPropagationLevel.Disabled)]
        [InlineData("full", DbmPropagationLevel.Full)]
        [InlineData("SERVICE", DbmPropagationLevel.Service)]
        public void DbmPropagationMode(string value, object expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DbmPropagationMode, value));
            var settings = new TracerSettings(source);

            settings.DbmPropagationMode.Should().Be((DbmPropagationLevel)expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void TraceId128BitGenerationEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, value));
            var settings = new TracerSettings(source);

            settings.TraceId128BitGenerationEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void TraceId128BitLoggingEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.TraceId128BitLoggingEnabled, value));
            var settings = new TracerSettings(source);

            settings.TraceId128BitLoggingEnabled.Should().Be(expected);
        }

        [Fact]
        public void RecordsTelemetryAboutChangesMadeInCode_PublicProperties()
        {
            const string serviceName = "someOtherName";
            var tracerSettings = new TracerSettings(NullConfigurationSource.Instance);

            tracerSettings.ServiceName = serviceName;
            var collector = new ConfigurationTelemetry();
            tracerSettings.CollectTelemetry(collector);
            var data = collector.GetData(); // defaults

            var configKeyValue = data
                                .GroupBy(x => x.Name)
                                .Should()
                                .ContainSingle(x => x.Key == ConfigurationKeys.ServiceName)
                                .Which
                                .OrderByDescending(x => x.SeqId)
                                .First();

            configKeyValue.Name.Should().Be(ConfigurationKeys.ServiceName);
            configKeyValue.Value.Should().Be(serviceName);
            configKeyValue.Origin.Should().Be(ConfigurationOrigins.Code.ToStringFast());
        }

        [Fact]
        public void RecordsTelemetryAboutChangesMadeInCode_InternalProperties()
        {
            const string serviceName = "someOtherName";
            var tracerSettings = new TracerSettings(NullConfigurationSource.Instance);

            tracerSettings.ServiceNameInternal = serviceName;
            var collector = new ConfigurationTelemetry();
            tracerSettings.CollectTelemetry(collector);
            var data = collector.GetData(); // defaults

            var configKeyValue = data
                                .GroupBy(x => x.Name)
                                .Should()
                                .ContainSingle(x => x.Key == ConfigurationKeys.ServiceName)
                                .Which
                                .OrderByDescending(x => x.SeqId)
                                .First();

            configKeyValue.Name.Should().Be(ConfigurationKeys.ServiceName);
            configKeyValue.Value.Should().Be(serviceName);
            configKeyValue.Origin.Should().Be(ConfigurationOrigins.Code.ToStringFast());
        }

        [Fact]
        public void RecordsTelemetryAboutTfm()
        {
            var tracerSettings = new TracerSettings(NullConfigurationSource.Instance);
            var collector = new ConfigurationTelemetry();
            tracerSettings.CollectTelemetry(collector);
            var data = collector.GetData();
            var value = data
                       .GroupBy(x => x.Name)
                       .Should()
                       .ContainSingle(x => x.Key == ConfigTelemetryData.ManagedTracerTfm)
                       .Which
                       .OrderByDescending(x => x.SeqId)
                       .First();

#if NET6_0_OR_GREATER
            var expected = "net6.0";
#elif NETCOREAPP3_1_OR_GREATER
            var expected = "netcoreapp3.1";
#elif NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_0
            var expected = "netstandard2.0";
#elif NETFRAMEWORK
            var expected = "net461";
#else
            #error Unexpected TFM
#endif
            value.Value.Should().Be(expected);
        }

        private void SetAndValidateStatusCodes(Action<TracerSettings, IEnumerable<int>> setStatusCodes, Func<TracerSettings, bool[]> getStatusCodes)
        {
            var settings = new TracerSettings();
            var statusCodes = new Queue<int>(new[] { 100, 201, 503 });

            setStatusCodes(settings, statusCodes);

            var result = getStatusCodes(settings);

            for (int i = 0; i < 600; i++)
            {
                if (result[i])
                {
                    var code = statusCodes.Dequeue();

                    Assert.Equal(code, i);
                }
            }

            Assert.Empty(statusCodes);
        }
    }
}
