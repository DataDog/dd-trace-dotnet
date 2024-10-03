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
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
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
        [InlineData(Tags.Env, "deployment.environment")]
        [InlineData(Tags.Version, "service.version")]
        public void OtelTagsSetsServiceInformation(string ddTagKey, string otelTagKey)
        {
            string expectedValue = $"ddtags-custom-{otelTagKey}";
            string tagsLine = $"{otelTagKey}=ddtags-custom-{otelTagKey}";
            var collection = new NameValueCollection { { ConfigurationKeys.OpenTelemetry.ResourceAttributes, tagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            Assert.True(settings.GlobalTags.Any());
            settings.GlobalTags.Should().NotContainKey(otelTagKey);

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(ddTagKey), expectedValue);
        }

        [Theory]
        [InlineData(Tags.Env, "deployment.environment")]
        [InlineData(Tags.Version, "service.version")]
        public void DDTagsTakesPrecedenceOverOtelTags(string ddTagKey, string otelTagKey)
        {
            string expectedValue = $"ddtags-custom-{ddTagKey}";
            string ddTagsLine = $"{ddTagKey}:ddtags-custom-{ddTagKey}";
            string otelTagsLine = $"{otelTagKey}=ddtags-custom-{otelTagKey}";
            var collection = new NameValueCollection { { ConfigurationKeys.GlobalTags, ddTagsLine }, { ConfigurationKeys.OpenTelemetry.ResourceAttributes, otelTagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            Assert.True(settings.GlobalTags.Any());
            settings.GlobalTags.Should().NotContainKey(otelTagKey);

            var tracer = new Tracer(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(ddTagKey), expectedValue);
        }

        [Theory]
        [InlineData("", null, true, null)]
        [InlineData("", "random", true, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("", "none", true, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("1", null, true, null)]
        [InlineData("1", "none", true, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("0", "random", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("0", "none", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData(null, "random", true, (int)Count.OpenTelemetryConfigInvalid)]
        [InlineData(null, "none", false, null)]
        public void TraceEnabled(string value, string otelValue, bool areTracesEnabled, int? metric)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.TraceEnabled, value },
                { ConfigurationKeys.OpenTelemetry.TracesExporter, otelValue },
            };

            var errorLog = new OverrideErrorLog();
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings), NullConfigurationTelemetry.Instance, errorLog);

            Assert.Equal(areTracesEnabled, tracerSettings.TraceEnabled);
            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.TracesExporter.ToLowerInvariant(), ConfigurationKeys.TraceEnabled.ToLowerInvariant());

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
        [InlineData("test", null, null, "test")]
        [InlineData("test", null, "ignored_otel", "test")]
        [InlineData("test", "error", "ignored_otel", "test")]
        [InlineData(null, "test", null, "test")]
        [InlineData(null, "test", "ignored_otel", "test")]
        [InlineData("", "test", "ignored_otel", "")]
        [InlineData(null, null, "otel", "otel")]
        [InlineData(null, null, null, null)]
        public void ServiceName(string value, string legacyValue, string otelValue, string expected)
        {
            const string legacyServiceName = "DD_SERVICE_NAME";
            const string otelKey = ConfigurationKeys.OpenTelemetry.ServiceName;

            var source = CreateConfigurationSource((ConfigurationKeys.ServiceName, value), (legacyServiceName, legacyValue), (otelKey, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.ServiceName.Should().Be(expected);
            Count? metric = otelValue switch
            {
                "ignored_otel" => Count.OpenTelemetryConfigHiddenByDatadogConfig,
                _ => null,
            };
            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.ServiceName.ToLowerInvariant(), ConfigurationKeys.ServiceName.ToLowerInvariant());
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
        [InlineData("key1:value1,key2:value2", "key3:value3", "otel_key=otel_value", new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,invalid,key2: value2", "key3:value3", "otel_key=otel_value", new[] { "key1:value1", "key2:value2" })]
        [InlineData("invalid", "key1:value1,key2:value2", "otel_key=otel_value", new string[0])]
        [InlineData(null, "key1:value1,key2:value2", "otel_key=otel_value", new[] { "key1:value1", "key2:value2" })]
        [InlineData("", "key1:value1,key2:value2", "otel_key=otel_value", new string[0])]
        [InlineData("", "", "otel_key=otel_value", new string[0])]
        [InlineData("invalid", "invalid", "otel_key=otel_value", new string[0])]
        [InlineData(null, null, "otel_key=otel_value", new[] { "otel_key:otel_value" })]
        public void GlobalTags(string value, string legacyValue, string otelValue, string[] expected)
        {
            const string legacyGlobalTagsKey = "DD_TRACE_GLOBAL_TAGS";
            const string otelKey = ConfigurationKeys.OpenTelemetry.ResourceAttributes;

            var source = CreateConfigurationSource((ConfigurationKeys.GlobalTags, value), (legacyGlobalTagsKey, legacyValue), (otelKey, otelValue));
            var settings = new TracerSettings(source);

            settings.GlobalTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        // null, empty, whitespace
        [InlineData(null, true, new string[0])]
        [InlineData("", true, new string[0])]
        [InlineData("   ", true, new string[0])]
        // nominal
        [InlineData("key1:value1,key2:value2,key3", true, new[] { "key1|value1", "key2|value2", "key3|" })]
        // trim whitespace
        [InlineData(" key1 : value1 ", true, new[] { "key1|value1" })]
        // other normalization
        [InlineData("key1:val.u e1?!", true, new[] { "key1|val.u e1?!" })]
        [InlineData("k.e y?!1", false, new[] { "k.e y?!1|" })]
        [InlineData(":leadingcolon", false, new string[0])]
        [InlineData(":leadingcolon", true, new string[0])]
        [InlineData("one:two:three", true, new[] { "one:two|three" })]
        public void HeaderTags(string value, bool normalizationFixEnabled, string[] expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.HeaderTags, value),
                (ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled, normalizationFixEnabled ? "1" : "0"));
            var settings = new TracerSettings(source);

            settings.HeaderTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Substring(0, v.IndexOf('|')), v => v.Substring(v.IndexOf('|') + 1)));
        }

        [Theory]
        // null, empty, whitespace
        [InlineData(null, true, new string[0])]
        [InlineData("", true, new string[0])]
        [InlineData("   ", true, new string[0])]
        // nominal
        [InlineData("key1:value1,key2:value2,key3", true, new[] { "key1:value1", "key2:value2", "key3:" })]
        // trim whitespace
        [InlineData(" key1 : value1 ", true, new[] { "key1:value1" })]
        // other normalization
        [InlineData("key1:val.u e1?!", true, new[] { "key1:val.u e1?!" })]
        [InlineData("k.e y?!1", false, new[] { "k.e y?!1:" })]
        public void GrpcTags(string value, bool normalizationFixEnabled, string[] expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.GrpcTags, value),
                (ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled, normalizationFixEnabled ? "1" : "0"));
            var settings = new TracerSettings(source);

            settings.GrpcTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
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
        [InlineData("true", "none", true)]
        [InlineData("true", "random", true)]
        [InlineData("true", null, true)]
        [InlineData("false", "none", false)]
        [InlineData("false", "random", false)]
        [InlineData("false", null, false)]
        [InlineData("A", "none", false)]
        [InlineData("A", "random", false)]
        [InlineData("", "none", false)]
        [InlineData("", "random", false)]
        [InlineData(null, "none", false)]
        [InlineData(null, "random", false)]
        [InlineData(null, null, false)]
        public void RuntimeMetricsEnabled(string value, string otelValue, bool expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.RuntimeMetricsEnabled, value),
                (ConfigurationKeys.OpenTelemetry.MetricsExporter, otelValue));

            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.RuntimeMetricsEnabled.Should().Be(expected);
            Count? metric = (value, otelValue) switch
            {
                (null, "random") => Count.OpenTelemetryConfigInvalid,
                (not null, not null) => Count.OpenTelemetryConfigHiddenByDatadogConfig,
                _ => null,
            };

            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.MetricsExporter.ToLowerInvariant(), ConfigurationKeys.RuntimeMetricsEnabled.ToLowerInvariant());
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
        [InlineData("glob", SamplingRulesFormat.Glob)]     // exact match
        [InlineData("Glob", SamplingRulesFormat.Glob)]     // case-insensitive
        [InlineData(" glob ", SamplingRulesFormat.Glob)]   // trim whitespace
        [InlineData("regex", SamplingRulesFormat.Regex)]   // exact match
        [InlineData("RegEx", SamplingRulesFormat.Regex)]   // case-insensitive
        [InlineData(" regex ", SamplingRulesFormat.Regex)] // trim whitespace
        [InlineData("none", SamplingRulesFormat.Unknown)]  // invalid
        [InlineData("1", SamplingRulesFormat.Unknown)]     // invalid
        [InlineData("", SamplingRulesFormat.Unknown)]      // empty is invalid
        [InlineData("  ", SamplingRulesFormat.Unknown)]    // whitespace is invalid
        [InlineData(null, SamplingRulesFormat.Glob)]       // null defaults to glob
        public void CustomSamplingRulesFormat(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CustomSamplingRulesFormat, value));
            var telemetry = new ConfigurationTelemetry();
            var settings = new TracerSettings(source, telemetry, new());

            // verify setting
            settings.CustomSamplingRulesFormat.Should().Be(expected);

            // verify telemetry
            var entries = telemetry.GetQueueForTesting()
                                   .Where(e => e is { Key: ConfigurationKeys.CustomSamplingRulesFormat })
                                   .OrderByDescending(e => e.SeqId)
                                   .ToList();

            // verify that we have 2 entries for this setting, one from code/default
            // and one calculated (the actual value we're using)
            entries.Should().HaveCount(2);

            // verify that the entry with the highest SeqId has the actual value we're using
            entries[0].StringValue.Should().Be(expected);

            foreach (var entry in entries)
            {
                switch (entry.Origin)
                {
                    case ConfigurationOrigins.Default:
                        // setting not specified, so the default value is used
                        entry.StringValue.Should().Be(SamplingRulesFormat.Glob);
                        break;
                    case ConfigurationOrigins.Code:
                        // original setting
                        entry.StringValue.Should().Be(value);
                        break;
                    case ConfigurationOrigins.Calculated:
                        // the actual setting used after normalization
                        entry.StringValue.Should().Be(expected);
                        break;
                    default:
                        throw new Exception($"Unexpected origin: {entry.Origin}");
                }
            }
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
        [InlineData("1.5", null, null, 1.5d)]
        [InlineData("1", "parentbased_traceidratio", "0.5", 1.0d)]
        [InlineData("0", "parentbased_traceidratio", null, 0.0d)]
        [InlineData("-1", "parentbased_traceidratio", null, -1.0d)]
        [InlineData("A", null, null, null)]
        [InlineData("", "parentbased_always_on", null, null)]
        [InlineData("", "parentbased_always_off", null, null)]
        [InlineData(null, "parentbased_traceidratio", "0.5", 0.5d)]
        [InlineData(null, "parentbased_traceidratio", "1", 1.0d)]
        [InlineData(null, "parentbased_traceidratio", null, null)]
        [InlineData(null, "traceidratio", "0.5", 0.5d)]
        [InlineData(null, "traceidratio", "1", 1.0d)]
        [InlineData(null, "traceidratio", null, null)]
        [InlineData(null, "parentbased_always_on", null, 1.0d)]
        [InlineData(null, "always_on", null, 1.0d)]
        [InlineData(null, "parentbased_always_off", null, 0.0d)]
        [InlineData(null, "always_off", null, 0.0d)]
        [InlineData(null, "traceidratio", "invalid", null)]
        [InlineData(null, "parentbased_always_on", "invalid", 1.0d)]
        [InlineData(null, "always_on", "invalid", 1.0d)]
        [InlineData(null, "parentbased_always_off", "invalid", 0.0d)]
        [InlineData(null, "always_off", "invalid", 0.0d)]
        [InlineData(null, "invalid", null, null)]
        [InlineData(null, "invalid", "invalid", null)]
        public void GlobalSamplingRate(string value, string otelSampler, string otelSampleRate, double? expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.GlobalSamplingRate, value),
                (ConfigurationKeys.OpenTelemetry.TracesSampler, otelSampler),
                (ConfigurationKeys.OpenTelemetry.TracesSamplerArg, otelSampleRate));
            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            // confirm the logs/metrics
            settings.GlobalSamplingRate.Should().Be(expected);
            var metrics = new List<(Count?, string, string)>();

            if (value is not null)
            {
                // hidden metrics
                if (otelSampler is not null)
                {
                    metrics.Add((Count.OpenTelemetryConfigHiddenByDatadogConfig, ConfigurationKeys.OpenTelemetry.TracesSampler.ToLowerInvariant(), ConfigurationKeys.GlobalSamplingRate.ToLowerInvariant()));
                }

                if (otelSampleRate is not null)
                {
                    metrics.Add((Count.OpenTelemetryConfigHiddenByDatadogConfig, ConfigurationKeys.OpenTelemetry.TracesSamplerArg.ToLowerInvariant(), ConfigurationKeys.GlobalSamplingRate.ToLowerInvariant()));
                }
            }
            else if (otelSampler is "invalid")
            {
                // we _don't_ report this one as invalid, and it "prevents" reporting the invalid arg
            }
            else if (otelSampler is "traceidratio" or "parentbased_traceidratio"
                  && otelSampleRate is "invalid" or null)
            {
                // we _only_ report this one if we need to use it
                metrics.Add((Count.OpenTelemetryConfigInvalid, ConfigurationKeys.OpenTelemetry.TracesSamplerArg.ToLowerInvariant(), ConfigurationKeys.GlobalSamplingRate.ToLowerInvariant()));
            }

            errorLog.ShouldHaveExpectedOtelMetric(metrics.ToArray());
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
        [MemberData(nameof(BooleanTestCases), true)]
        public void DelayWcfInstrumentationEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.DelayWcfInstrumentationEnabled, value));
            var settings = new TracerSettings(source);

            settings.DelayWcfInstrumentationEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void WcfWebHttpResourceNamesEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.WcfWebHttpResourceNamesEnabled, value));
            var settings = new TracerSettings(source);

            settings.WcfWebHttpResourceNamesEnabled.Should().Be(expected);
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
        [InlineData("1", "0", "otel_ignored", true)]
        [InlineData("0", "0", null, false)]
        [InlineData("true", "1", null, true)]
        [InlineData("false", "1", null, false)]
        [InlineData(null, "1", "otel_ignored", true)]
        [InlineData(null, "true", null, true)]
        [InlineData(null, "0", null, false)]
        [InlineData(null, "false", "otel_ignored", false)]
        [InlineData(null, null, "true", false)]
        [InlineData(null, null, "uses_default_value", false)]
        [InlineData("", "", null, false)]
        public void IsActivityListenerEnabled(string value, string fallbackValue, string otelValue, bool expected)
        {
            const string fallbackKey = "DD_TRACE_ACTIVITY_LISTENER_ENABLED";
            const string otelKey = ConfigurationKeys.OpenTelemetry.SdkDisabled;

            var source = CreateConfigurationSource((ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, value), (fallbackKey, fallbackValue), (otelKey, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.IsActivityListenerEnabled.Should().Be(expected);
            Count? metric = (value ?? fallbackValue, otelValue?.ToLower()) switch
            {
                (null, "true") => null,
                (null, _) => Count.OpenTelemetryConfigInvalid,
                (not null, not null) => Count.OpenTelemetryConfigHiddenByDatadogConfig,
                _ => null,
            };

            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.SdkDisabled.ToLowerInvariant(), ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled.ToLowerInvariant());
        }

        [Theory]
        [InlineData("test1,, ,test2", "test3,, ,test4", "test5,, ,test6", null, new[] { "test1", "test2" })]
        [InlineData("", "test3,, ,test4", "test5,, ,test6", null, new[] { "Datadog", "tracecontext" })]
        [InlineData(null, "test3,, ,test4", "test5,, ,test6", null, new[] { "test3", "test4" })]
        [InlineData(null, null, "test5,, ,test6", null, new[] { "test5", "test6" })]
        [InlineData(null, null, null, "tracecontext,datadog", new[] { "tracecontext", "datadog" })]
        [InlineData(null, null, null, "tracecontext", new[] { "tracecontext" })]
        [InlineData(null, null, null, "tracecontext,b3,b3multi", new[] { "tracecontext", "b3 single header", "b3multi" })]
        [InlineData(null, null, null, null, new[] { "Datadog", "tracecontext" })]
        [InlineData(null, null, null, ",", new[] { "Datadog", "tracecontext" })]
        public void PropagationStyleInject(string value, string legacyValue, string fallbackValue, string otelValue, string[] expected)
        {
            const string legacyKey = "DD_PROPAGATION_STYLE_INJECT";
            const string otelKey = ConfigurationKeys.OpenTelemetry.Propagators;

            foreach (var isActivityListenerEnabled in new[] { true, false })
            {
                var source = CreateConfigurationSource(
                    (ConfigurationKeys.PropagationStyleInject, value),
                    (legacyKey, legacyValue),
                    (ConfigurationKeys.PropagationStyle, fallbackValue),
                    (otelKey, otelValue),
                    (ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, isActivityListenerEnabled ? "1" : "0"));

                var errorLog = new OverrideErrorLog();
                var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);

                settings.PropagationStyleInject.Should().BeEquivalentTo(expected);

                Count? metric = (value ?? legacyValue ?? fallbackValue, otelValue) switch
                {
                    (null, ",") => Count.OpenTelemetryConfigInvalid,
                    (not null, not null) => Count.OpenTelemetryConfigHiddenByDatadogConfig,
                    _ => null,
                };

                errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.Propagators.ToLowerInvariant(), ConfigurationKeys.PropagationStyle.ToLowerInvariant());
            }
        }

        [Theory]
        [InlineData("test1,, ,test2", "test3,, ,test4", "test5,, ,test6", null, new[] { "test1", "test2" })]
        [InlineData("", "test3,, ,test4", "test5,, ,test6", null, new[] { "Datadog", "tracecontext" })]
        [InlineData(null, "test3,, ,test4", "test5,, ,test6", null, new[] { "test3", "test4" })]
        [InlineData(null, null, "test5,, ,test6", null, new[] { "test5", "test6" })]
        [InlineData(null, null, null, "tracecontext,datadog", new[] { "tracecontext", "datadog" })]
        [InlineData(null, null, null, "tracecontext", new[] { "tracecontext" })]
        [InlineData(null, null, null, "tracecontext,b3,b3multi", new[] { "tracecontext", "b3 single header", "b3multi" })]
        [InlineData(null, null, null, null, new[] { "Datadog", "tracecontext" })]
        [InlineData(null, null, null, ",", new[] { "Datadog", "tracecontext" })]
        public void PropagationStyleExtract(string value, string legacyValue, string fallbackValue, string otelValue, string[] expected)
        {
            const string legacyKey = "DD_PROPAGATION_STYLE_EXTRACT";
            const string otelKey = ConfigurationKeys.OpenTelemetry.Propagators;

            foreach (var isActivityListenerEnabled in new[] { true, false })
            {
                var source = CreateConfigurationSource(
                    (ConfigurationKeys.PropagationStyleExtract, value),
                    (legacyKey, legacyValue),
                    (ConfigurationKeys.PropagationStyle, fallbackValue),
                    (otelKey, otelValue),
                    (ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, isActivityListenerEnabled ? "1" : "0"));

                var errorLog = new OverrideErrorLog();
                var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);

                settings.PropagationStyleExtract.Should().BeEquivalentTo(expected);

                Count? metric = (value ?? legacyValue ?? fallbackValue, otelValue) switch
                {
                    (null, ",") => Count.OpenTelemetryConfigInvalid,
                    (not null, not null) => Count.OpenTelemetryConfigHiddenByDatadogConfig,
                    _ => null,
                };

                errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.Propagators.ToLowerInvariant(), ConfigurationKeys.PropagationStyle.ToLowerInvariant());
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
        [MemberData(nameof(BooleanTestCases), true)]
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

        // profiling takes precedence over SSI
        // "auto" is a special profiling value that enables profiling when deployed via SSI
        // the profiler will also be enabled when "profiler" will be added to the DD_INJECTION_ENABLED environment variable
        [Theory]
        [InlineData("1", null, true)]
        [InlineData("0", null, false)]
        [InlineData("true", null, true)]
        [InlineData("false", null, false)]
        [InlineData("auto", null, true)]
        [InlineData("1", "not used", true)]
        [InlineData("0", "not used", false)]
        [InlineData("true", "not used", true)]
        [InlineData("false", "not used", false)]
        [InlineData("auto", "not used", true)]
        [InlineData("invalid", "foo, profiler, bar", true)]
        [InlineData("invalid", "anything else", true)]
        [InlineData("invalid", "", true)]
        [InlineData("invalid", null, false)]
        [InlineData("", "foo, profiler, bar", true)]
        [InlineData("", "anything else", true)]
        [InlineData("", "", true)]
        [InlineData("", null, false)]
        [InlineData(null, "foo, profiler, bar", true)]
        [InlineData(null, "anything else", true)]
        [InlineData(null, null, false)]
        [InlineData(null, "", true)]
        public void ProfilingEnabled(string profilingValue, string ssiValue, bool expected)
        {
            var values = new List<(string, string)>();
            if (profilingValue is not null)
            {
                values.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilingEnabled, profilingValue));
            }

            if (ssiValue is not null)
            {
                values.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.SsiDeployed, ssiValue));
            }

            var source = CreateConfigurationSource(values.ToArray());
            var settings = new TracerSettings(source);

            settings.ProfilingEnabledInternal.Should().Be(expected);
        }

        [Theory]
        // null, empty, whitespace
        [InlineData(null, true, true, "")]
        [InlineData("", true, true, "")]
        [InlineData("  ", true, true, "")]
        // no normalization
        [InlineData("my-tag", true, true, "my-tag")]
        [InlineData("my.tag", true, true, "my.tag")]
        [InlineData("my tag", true, true, "my tag")]
        [InlineData("my/!*&tag", true, true, "my/!*&tag")]
        [InlineData("1my-tag", true, false, null)]
        // opt-in to previous behavior: normalize, but keep spaces
        [InlineData("my-tag", false, true, "my-tag")]
        [InlineData("my.tag", false, true, "my_tag")]
        [InlineData("my tag", false, true, "my tag")]
        [InlineData("my:/!*&tag", false, true, "my:/___tag")]
        [InlineData("1my-tag", false, false, null)]
        public void InitializeHeaderTag(string tagName, bool headerTagsNormalizationFixEnabled, bool expectedValid, string expectedTagName)
        {
            TracerSettings.InitializeHeaderTag(tagName, headerTagsNormalizationFixEnabled, out var finalTagName)
                          .Should()
                          .Be(expectedValid);

            finalTagName.Should().Be(expectedTagName);
        }

        [Theory]
        [InlineData(null, null, "500-599")]
        [InlineData(null, "400", "400")]
        [InlineData("444", null, "444")]
        [InlineData("444", "424", "444")]
        public void ValidateServerErrorStatusCodes(string newServerErrorKeyValue, string deprecatedServerErrorKeyValue, string expectedServerErrorCodes)
        {
            const string httpServerErrorStatusCodes = "DD_TRACE_HTTP_SERVER_ERROR_STATUSES";
            const string deprecatedHttpServerErrorStatusCodes = "DD_HTTP_SERVER_ERROR_STATUSES";

            var source = CreateConfigurationSource(
                (httpServerErrorStatusCodes, newServerErrorKeyValue),
                (deprecatedHttpServerErrorStatusCodes, deprecatedServerErrorKeyValue));

            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);
            var result = settings.HttpServerErrorStatusCodes;

            ValidateErrorStatusCodes(result, newServerErrorKeyValue, deprecatedServerErrorKeyValue, expectedServerErrorCodes);
        }

        [Theory]
        [InlineData(null, null, "400-499")]
        [InlineData(null, "500", "500")]
        [InlineData("555", null, "555")]
        [InlineData("555", "525", "555")]
        public void ValidateClientErrorStatusCodes(string newClientErrorKeyValue, string deprecatedClientErrorKeyValue, string expectedClientErrorCodes)
        {
            const string httpClientErrorStatusCodes = "DD_TRACE_HTTP_CLIENT_ERROR_STATUSES";
            const string deprecatedHttpClientErrorStatusCodes = "DD_HTTP_CLIENT_ERROR_STATUSES";

            var source = CreateConfigurationSource(
                (httpClientErrorStatusCodes, newClientErrorKeyValue),
                (deprecatedHttpClientErrorStatusCodes, deprecatedClientErrorKeyValue));

            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);
            var result = settings.HttpClientErrorStatusCodes;

            ValidateErrorStatusCodes(result, newClientErrorKeyValue, deprecatedClientErrorKeyValue, expectedClientErrorCodes);
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

        private void ValidateErrorStatusCodes(bool[] result, string newErrorKeyValue, string deprecatedErrorKeyValue, string expectedErrorRange)
        {
            if (newErrorKeyValue is not null || deprecatedErrorKeyValue is not null)
            {
                Assert.True(result[int.Parse(expectedErrorRange)]);
            }
            else
            {
                var statusCodeLimitsRange = expectedErrorRange.Split('-');
                for (var i = int.Parse(statusCodeLimitsRange[0]); i <= int.Parse(statusCodeLimitsRange[1]); i++)
                {
                    Assert.True(result[i]);
                }
            }
        }
    }
}
