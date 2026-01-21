// <copyright file="MutableSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class MutableSettingsTests : SettingsTestsBase
    {
        private readonly Mock<IAgentWriter> _writerMock;
        private readonly Mock<ITraceSampler> _samplerMock;

        public MutableSettingsTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            _samplerMock = new Mock<ITraceSampler>();
        }

        [Fact]
        public void Equality()
        {
            var properties = typeof(MutableSettings)
                            .GetProperties()
                            .Select(x => x.Name)
                            .ToList();

            properties.Should().NotBeEmpty();

            // precondition checks
            GetSettings(string.Empty, null)
               .Equals(GetSettings(string.Empty, null))
               .Should()
               .BeTrue("when there's no config, two settings objects should be equal");

            foreach (var test in GetTestValues())
            {
                var settings1 = GetSettings(test.Key, test.Value1);
                var settings2 = GetSettings(test.Key, test.Value1);
                var settings3 = GetSettings(test.Key, test.Value2);

                settings1.Equals(settings2).Should().BeTrue($"the same {test.Property} should give the same result");
                settings1.Equals(settings3).Should().BeFalse($"changing {test.Property} should change equality");

                properties.Remove(test.Property).Should().BeTrue($"the {test.Property} property should exist");

                settings1.GetHashCode().Should().Be(settings2.GetHashCode());
            }

            // TODO: test that changing this changes the settings. Currently this is always false, so can't test it
            properties.Remove(nameof(MutableSettings.CustomSamplingRulesIsRemote));

            // This property depends directly on ServiceName, and so isn't required to be part of the equality tests
            // We _could_ add it though if that's preferable
            properties.Remove(nameof(MutableSettings.DefaultServiceName));
            properties.Should().BeEmpty("should compare all properties as part of MutableSettings, but some properties were not used");
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env, null)]
        [InlineData(ConfigurationKeys.Environment, Tags.Env, "custom-env")]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version, null)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version, "custom-version")]
        public async Task ConfiguredTracerSettings_DefaultTagsSetFromEnvironmentVariable(string environmentVariableKey, string tagKey, string value)
        {
            var collection = new NameValueCollection { { environmentVariableKey, value } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            await using var tracer = TracerHelper.Create(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(tagKey), value);
        }

        [Theory]
        [InlineData(ConfigurationKeys.Environment, Tags.Env)]
        [InlineData(ConfigurationKeys.ServiceVersion, Tags.Version)]
        public async Task DDVarTakesPrecedenceOverDDTags(string envKey, string tagKey)
        {
            string envValue = $"ddenv-custom-{tagKey}";
            string tagsLine = $"{tagKey}:ddtags-custom-{tagKey}";
            var collection = new NameValueCollection { { envKey, envValue }, { ConfigurationKeys.GlobalTags, tagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);

            await using var tracer = TracerHelper.Create(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(tagKey), envValue);
        }

        [Theory]
        [InlineData(Tags.Env, "deployment.environment")]
        [InlineData(Tags.Version, "service.version")]
        public async Task OtelTagsSetsServiceInformation(string ddTagKey, string otelTagKey)
        {
            string expectedValue = $"ddtags-custom-{otelTagKey}";
            string tagsLine = $"{otelTagKey}=ddtags-custom-{otelTagKey}";
            var collection = new NameValueCollection { { ConfigurationKeys.OpenTelemetry.ResourceAttributes, tagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.GlobalTags.Should().NotContainKey(otelTagKey);

            await using var tracer = TracerHelper.Create(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("Operation");

            Assert.Equal(span.GetTag(ddTagKey), expectedValue);
        }

        [Theory]
        [InlineData(Tags.Env, "deployment.environment")]
        [InlineData(Tags.Version, "service.version")]
        public async Task DDTagsTakesPrecedenceOverOtelTags(string ddTagKey, string otelTagKey)
        {
            string expectedValue = $"ddtags-custom-{ddTagKey}";
            string ddTagsLine = $"{ddTagKey}:ddtags-custom-{ddTagKey}";
            string otelTagsLine = $"{otelTagKey}=ddtags-custom-{otelTagKey}";
            var collection = new NameValueCollection { { ConfigurationKeys.GlobalTags, ddTagsLine }, { ConfigurationKeys.OpenTelemetry.ResourceAttributes, otelTagsLine } };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.GlobalTags.Should().NotContainKey(otelTagKey);

            await using var tracer = TracerHelper.Create(settings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
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
        public async Task TraceEnabled(string value, string otelValue, bool areTracesEnabled, int? metric)
        {
            var settings = new NameValueCollection
            {
                { ConfigurationKeys.TraceEnabled, value },
                { ConfigurationKeys.OpenTelemetry.TracesExporter, otelValue },
            };

            var errorLog = new OverrideErrorLog();
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(settings), NullConfigurationTelemetry.Instance, errorLog);

            Assert.Equal(areTracesEnabled, tracerSettings.Manager.InitialMutableSettings.TraceEnabled);
            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.TracesExporter.ToLowerInvariant(), ConfigurationKeys.TraceEnabled.ToLowerInvariant());

            _writerMock.Invocations.Clear();

            await using var tracer = TracerHelper.Create(tracerSettings, _writerMock.Object, _samplerMock.Object, scopeManager: null, statsd: null);
            var span = tracer.StartSpan("TestTracerDisabled");
            span.Dispose();

            var assertion = areTracesEnabled ? Times.Once() : Times.Never();

            _writerMock.Verify(w => w.WriteTrace(in It.Ref<SpanCollection>.IsAny), assertion);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), null, Strings.DisallowEmpty)]
        public void Environment(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Environment, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.Environment.Should().Be(expected);
        }

        [Theory]
        [InlineData("test", null, null, "test")]
        // This is the current behaviour - _should_ it be, or should the result be normalized, is a separate question...
        [InlineData("My Service Name!", null, null, "My Service Name!")]
        [InlineData("test", null, "ignored_otel", "test")]
        [InlineData("test", "error", "ignored_otel", "test")]
        [InlineData(null, "test", null, "test")]
        [InlineData(null, "test", "ignored_otel", "test")]
        [InlineData("", "test", "ignored_otel", null)]
        [InlineData(null, null, "otel", "otel")]
        [InlineData(null, null, null, null)]
        public void ServiceName(string value, string legacyValue, string otelValue, string expected)
        {
            const string legacyServiceName = "DD_SERVICE_NAME";
            const string otelKey = ConfigurationKeys.OpenTelemetry.ServiceName;

            var source = CreateConfigurationSource((ConfigurationKeys.ServiceName, value), (legacyServiceName, legacyValue), (otelKey, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);
            var mutable = GetMutableSettings(source, settings);

            mutable.ServiceName.Should().Be(expected);
            Count? metric = otelValue switch
            {
                "ignored_otel" => Count.OpenTelemetryConfigHiddenByDatadogConfig,
                _ => null,
            };
            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.ServiceName.ToLowerInvariant(), ConfigurationKeys.ServiceName.ToLowerInvariant());
        }

        [Theory]
        [MemberData(nameof(StringTestCases), null, Strings.DisallowEmpty)]
        public void ServiceVersion(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ServiceVersion, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.ServiceVersion.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), null, Strings.DisallowEmpty)]
        public void GitCommitSha(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.GitCommitSha, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.GitCommitSha.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), null, Strings.DisallowEmpty)]
        public void GitRepositoryUrl(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.GitRepositoryUrl, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.GitRepositoryUrl.Should().Be(expected);
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
            var mutable = GetMutableSettings(source, settings);

            mutable.DisabledIntegrationNames.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void AnalyticsEnabled(string value, bool expected)
        {
#pragma warning disable 618 // App analytics is deprecated, but still used
            var source = CreateConfigurationSource((ConfigurationKeys.GlobalAnalyticsEnabled, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.AnalyticsEnabled.Should().Be(expected);
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
            var mutable = GetMutableSettings(source, settings);

            mutable.Integrations[IntegrationRegistry.Names[0]].Enabled.Should().BeNull();
            mutable.Integrations[IntegrationRegistry.Names[1]].Enabled.Should().BeFalse();
            mutable.Integrations[IntegrationRegistry.Names[2]].Enabled.Should().BeTrue();
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
            var mutable = GetMutableSettings(source, settings);

            mutable.MaxTracesSubmittedPerSecond.Should().Be(expected);
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
            var mutable = GetMutableSettings(source, settings);

            mutable.GlobalTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
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
            var mutable = GetMutableSettings(source, settings);

            mutable.HeaderTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Substring(0, v.IndexOf('|')), v => v.Substring(v.IndexOf('|') + 1)));
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
            var mutable = GetMutableSettings(source, settings);

            mutable.GrpcTags.Should().BeEquivalentTo(expected.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [InlineData("key1:value1,key2:value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("key1 :value1,invalid,key2: value2", new[] { "key1:value1", "key2:value2" })]
        [InlineData("invalid", new string[0])]
        [InlineData(null, new string[0])]
        [InlineData("", new string[0])]
        public void ServiceNameMappings(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ServiceNameMappings, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.ServiceNameMappings.Should().BeEquivalentTo(expected?.ToDictionary(v => v.Split(':').First(), v => v.Split(':').Last()));
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void TracerMetricsEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.TracerMetricsEnabled, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.TracerMetricsEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CustomSamplingRules(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CustomSamplingRules, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.CustomSamplingRules.Should().Be(expected);
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
            var mutable = GetMutableSettings(source, settings);

            // confirm the logs/metrics
            mutable.GlobalSamplingRate.Should().Be(expected);
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
            var mutable = GetMutableSettings(source, settings);

            mutable.StartupDiagnosticLogEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void KafkaCreateConsumerScopeEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.KafkaCreateConsumerScopeEnabled, value));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.KafkaCreateConsumerScopeEnabled.Should().Be(expected);
        }

        [Fact]
        public void DisableTracerIfNoApiKeyInAas()
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.SiteNameKey, "site-name"));
            var settings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, settings);

            mutable.TraceEnabled.Should().BeFalse();
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
            var tracerSettings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, tracerSettings);
            var result = mutable.HttpServerErrorStatusCodes;

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
            var tracerSettings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, tracerSettings);
            var result = mutable.HttpClientErrorStatusCodes;

            ValidateErrorStatusCodes(result, newClientErrorKeyValue, deprecatedClientErrorKeyValue, expectedClientErrorCodes);
        }

        [Fact]
        public void DDTagsSetsServiceInformation()
        {
            var source = new NameValueConfigurationSource(new()
            {
                { "DD_TAGS", "env:datadog_env,service:datadog_service,version:datadog_version,git.repository_url:https://Myrepository,git.commit.sha:42" },
            });

            var tracerSettings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, tracerSettings);

            mutable.Environment.Should().Be("datadog_env");
            mutable.ServiceVersion.Should().Be("datadog_version");
            mutable.ServiceName.Should().Be("datadog_service");
            mutable.GitRepositoryUrl.Should().Be("https://Myrepository");
            mutable.GitCommitSha.Should().Be("42");
        }

        [Fact]
        public void OTELTagsSetsServiceInformation()
        {
            var source = new NameValueConfigurationSource(new()
            {
                { "OTEL_RESOURCE_ATTRIBUTES", "deployment.environment=datadog_env,service.name=datadog_service,service.version=datadog_version" },
            });

            var tracerSettings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, tracerSettings);

            mutable.Environment.Should().Be("datadog_env");
            mutable.ServiceVersion.Should().Be("datadog_version");
            mutable.ServiceName.Should().Be("datadog_service");
        }

        [Fact]
        public void DDTagsTakesPrecedenceOverOTELTags()
        {
            var source = new NameValueConfigurationSource(new()
            {
                { "DD_TAGS", "env:datadog_env" },
                { "OTEL_RESOURCE_ATTRIBUTES", "deployment.environment=datadog_env,service.name=datadog_service,service.version=datadog_version" },
            });

            var errorLog = new OverrideErrorLog();
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, errorLog);
            var mutable = GetMutableSettings(source, tracerSettings);

            mutable.Environment.Should().Be("datadog_env");

            // Since the DD_TAGS config is set, the OTEL_RESOURCE_ATTRIBUTES config is ignored
            mutable.ServiceVersion.Should().NotBe("datadog_version");
            mutable.ServiceName.Should().NotBe("datadog_service");
            errorLog.ShouldHaveExpectedOtelMetric(Count.OpenTelemetryConfigHiddenByDatadogConfig, "OTEL_RESOURCE_ATTRIBUTES".ToLowerInvariant(), "DD_TAGS".ToLowerInvariant());
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void LogsInjectionEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.LogsInjectionEnabled, value));
            var tracerSettings = new TracerSettings(source);
            var mutable = GetMutableSettings(source, tracerSettings);

            mutable.LogsInjectionEnabled.Should().Be(expected);
        }

        private static (string Key, string Property, object Value1, object Value2)[] GetTestValues()
            =>
            [
                (ConfigurationKeys.TraceEnabled, nameof(MutableSettings.TraceEnabled), true, false),
                (ConfigurationKeys.CustomSamplingRules, nameof(MutableSettings.CustomSamplingRules), "a", "b"),
                // (ConfigurationKeys.CustomSamplingRulesIsRemote, nameof(MutableSettings.CustomSamplingRulesIsRemote), true, false),
                (ConfigurationKeys.GlobalSamplingRate, nameof(MutableSettings.GlobalSamplingRate), 0.2, 0.3),
                (ConfigurationKeys.LogsInjectionEnabled, nameof(MutableSettings.LogsInjectionEnabled), true, false),
                (ConfigurationKeys.GlobalTags, nameof(MutableSettings.GlobalTags), "a:b", "c:d"),
                (ConfigurationKeys.HeaderTags, nameof(MutableSettings.HeaderTags), "a", "b"),
                (ConfigurationKeys.StartupDiagnosticLogEnabled, nameof(MutableSettings.StartupDiagnosticLogEnabled), true, false),
                (ConfigurationKeys.Environment, nameof(MutableSettings.Environment), "a", "b"),
                (ConfigurationKeys.ServiceName, nameof(MutableSettings.ServiceName), "a", "b"),
                (ConfigurationKeys.ServiceVersion, nameof(MutableSettings.ServiceVersion), "a", "b"),
                (ConfigurationKeys.DisabledIntegrations, nameof(MutableSettings.DisabledIntegrationNames), "a", "b"),
                (ConfigurationKeys.GrpcTags, nameof(MutableSettings.GrpcTags), "a", "b"),
                (ConfigurationKeys.TracerMetricsEnabled, nameof(MutableSettings.TracerMetricsEnabled), true, false),
                ("DD_TRACE_Process_ENABLED", nameof(MutableSettings.Integrations), true, false),
#pragma warning disable CS0618 // Type or member is obsolete
                (ConfigurationKeys.GlobalAnalyticsEnabled, nameof(MutableSettings.AnalyticsEnabled), true, false),
                (ConfigurationKeys.MaxTracesSubmittedPerSecond, nameof(MutableSettings.MaxTracesSubmittedPerSecond), 10, 20),
#pragma warning restore CS0618 // Type or member is obsolete
                (ConfigurationKeys.KafkaCreateConsumerScopeEnabled, nameof(MutableSettings.KafkaCreateConsumerScopeEnabled), true, false),
                (ConfigurationKeys.HttpServerErrorStatusCodes, nameof(MutableSettings.HttpServerErrorStatusCodes), "400-499", "400-599"),
                (ConfigurationKeys.HttpClientErrorStatusCodes, nameof(MutableSettings.HttpClientErrorStatusCodes), "400-499", "400-599"),
                (ConfigurationKeys.ServiceNameMappings, nameof(MutableSettings.ServiceNameMappings), "a:b", "c:d"),
                (ConfigurationKeys.CIVisibility.GitRepositoryUrl, nameof(MutableSettings.GitRepositoryUrl), "a", "b"),
                (ConfigurationKeys.CIVisibility.GitCommitSha, nameof(MutableSettings.GitCommitSha), "a", "b"),
            ];

        private static MutableSettings GetSettings(string key, object value)
        {
            var source = new DictionaryConfigurationSource(new Dictionary<string, string> { { key, value?.ToString() } });
            return MutableSettings.CreateInitialMutableSettings(
                source,
                NullConfigurationTelemetry.Instance,
                new OverrideErrorLog(),
                new TracerSettings());
        }

        private static MutableSettings GetMutableSettings(IConfigurationSource source, TracerSettings tracerSettings)
            => MutableSettings.CreateInitialMutableSettings(
                source,
                NullConfigurationTelemetry.Instance,
                new OverrideErrorLog(),
                tracerSettings);

        private static void ValidateErrorStatusCodes(bool[] result, string newErrorKeyValue, string deprecatedErrorKeyValue, string expectedErrorRange)
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
