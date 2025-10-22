// <copyright file="SettingsInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.TracerSettings;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;
using CtorIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer.CtorIntegration;
using ImmutableManualSettings = DatadogTraceManual::Datadog.Trace.Configuration.ImmutableTracerSettings;
using ManualSettings = DatadogTraceManual::Datadog.Trace.Configuration.TracerSettings;

namespace Datadog.Trace.Tests.ManualInstrumentation;

#pragma warning disable CS0618 // Type or member is obsolete
public class SettingsInstrumentationTests
{
    [Fact]
    public void AutomaticToManual_AllDefaultSettingsAreTransferredCorrectly()
    {
        var automatic = new TracerSettings();
        Dictionary<string, object> serializedSettings = new();
        PopulateDictionaryIntegration.PopulateSettings(serializedSettings, automatic);

        var manual = new ManualSettings(serializedSettings, isFromDefaultSources: false);

        AssertEquivalent(manual, automatic);
    }

    [Fact]
    public void AutomaticToManual_IncludesAllExpectedKeys()
    {
        var automatic = new TracerSettings();
        Dictionary<string, object> serializedSettings = new();
        PopulateDictionaryIntegration.PopulateSettings(serializedSettings, automatic);

        // ensure that we have all the expected keys
        var keys = GetAutomaticTracerSettingKeys();
        serializedSettings.Should().ContainKeys(keys).And.HaveSameCount(keys);
    }

    [Fact]
    public void AutomaticToManual_CustomSettingsAreTransferredCorrectly()
    {
        var automatic = GetAndAssertAutomaticTracerSettings();

        Dictionary<string, object> serializedSettings = new();
        PopulateDictionaryIntegration.PopulateSettings(serializedSettings, automatic);

        var manual = new ManualSettings(serializedSettings, isFromDefaultSources: false);

        AssertEquivalent(manual, automatic);
    }

    [Fact]
    public void ManualToAutomatic_IncludesNoKeysWhenNotChanged()
    {
        var manual = new ManualSettings(new(), isFromDefaultSources: false);
        var settings = manual.ToDictionary();

        settings.Should()
                .ContainSingle()
                .And.Contain(TracerSettingKeyConstants.IsFromDefaultSourcesKey, value: false);
    }

    [Fact]
    public void ManualToAutomatic_IncludesAllExpectedKeys()
    {
        // change all the defaults to make sure we add the keys to the dictionary
        Dictionary<string, object> originalSettings = new();
        PopulateDictionaryIntegration.PopulateSettings(originalSettings, new TracerSettings());
        var manual = new ManualSettings(originalSettings, isFromDefaultSources: false)
        {
            AgentUri = new Uri("http://localhost:1234"),
            AnalyticsEnabled = true,
            CustomSamplingRules = """[{"sample_rate":0.3, "service":"shopping-cart.*"}]""",
            DiagnosticSourceEnabled = true,
            DisabledIntegrationNames = ["something", nameof(IntegrationId.Kafka)],
            Environment = "my-test-env",
            GlobalSamplingRate = 0.5,
            GlobalTags = new Dictionary<string, string> { { "tag1", "value" } },
            GrpcTags = new Dictionary<string, string> { { "grpc1", "grpc-value" } },
            HeaderTags = new Dictionary<string, string> { { "header1", "header-value" } },
            KafkaCreateConsumerScopeEnabled = false,
            LogsInjectionEnabled = true,
            MaxTracesSubmittedPerSecond = 50,
            ServiceName = "my-test-service",
            ServiceVersion = "1.2.3",
            StartupDiagnosticLogEnabled = false,
            StatsComputationEnabled = true,
            TraceEnabled = false,
            TracerMetricsEnabled = true,
        };

        manual.Integrations[nameof(IntegrationId.Couchbase)].AnalyticsSampleRate = 0.5;

        manual.SetServiceNameMappings(new Dictionary<string, string> { { "some-service", "some-mapping" } });
        manual.SetHttpClientErrorStatusCodes([400, 401, 402]);
        manual.SetHttpServerErrorStatusCodes([500, 501, 502]);

        var settings = manual.ToDictionary();

        var keys = GetManualTracerSettingKeys();
        settings.Should().ContainKeys(keys);

        // we have additional keys for each of the customized settings
        settings.Keys.Except(keys).Should().ContainSingle("DD_TRACE_COUCHBASE_ANALYTICS_SAMPLE_RATE");
    }

    [Fact]
    public void ManualInstrumentationLegacyConfigurationSource_ProducesPublicApiForEveryExpectedValue()
    {
        // If we add more public API properties, we need to exclude them here, as we do _not_ expect the legacy provider to support them.
        string[] excluded = [TracerSettingKeyConstants.IntegrationSettingsKey, TracerSettingKeyConstants.IsFromDefaultSourcesKey];
        var keys = GetManualTracerSettingKeys().Where(x => !excluded.Contains(x));
        Dictionary<string, PublicApiUsage> results = new();
        foreach (var key in keys)
        {
            var result = ManualInstrumentationLegacyConfigurationSource.GetTelemetryKey(key);
            result.Should().NotBeNull($"the TracerSettingKeyConstants key '{key}' should correspond to a public API property. " +
                                      "Note that if you have added a _new_ TracerSettingKeyConstants value, this failure is expected - " +
                                      "you should not update your implementation, instead you should add to the 'exlude' list in this test");
            results.Add(key, result!.Value);
        }

        results.Should().OnlyHaveUniqueItems(x => x.Value);
    }

    [Fact]
    public void ManualInstrumentationConfigurationSource_ProducesPublicApiForEveryExpectedValue()
    {
        // Do not add more values here, unless they represent "meta" properties like "IsFromDefaultSources" etc
        string[] excluded = [TracerSettingKeyConstants.IntegrationSettingsKey, TracerSettingKeyConstants.IsFromDefaultSourcesKey];
        var keys = GetManualTracerSettingKeys().Where(x => !excluded.Contains(x));
        Dictionary<string, PublicApiUsage> results = new();
        foreach (var key in keys)
        {
            var result = ManualInstrumentationConfigurationSource.GetTelemetryKey(key);
            result.Should()
                  .NotBeNull(
                       $"the TracerSettingKeyConstants key '{key}' should correspond to a public API property. " +
                       "Update ManualInstrumentationConfigurationSource.GetTelemetryKey() to include a mapping for the key.");
            results.Add(key, result!.Value);
        }

        results.Should().OnlyHaveUniqueItems(x => x.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ManualToAutomatic_CustomSettingsAreTransferredCorrectly(bool useLegacySettings)
    {
        Dictionary<string, object> initialValues = new();
        PopulateDictionaryIntegration.PopulateSettings(initialValues, new TracerSettings());

        var manual = new ManualSettings(initialValues, isFromDefaultSources: false)
        {
            AgentUri = new Uri("http://localhost:1234"),
            AnalyticsEnabled = true,
            CustomSamplingRules = """[{"sample_rate":0.3, "service":"shopping-cart.*"}]""",
            DiagnosticSourceEnabled = true,
            DisabledIntegrationNames = ["something", nameof(IntegrationId.Kafka)],
            Environment = "my-test-env",
            GlobalSamplingRate = 0.5,
            GlobalTags = new Dictionary<string, string> { { "tag1", "value" } },
            GrpcTags = new Dictionary<string, string> { { "grpc1", "grpc-value" } },
            HeaderTags = new Dictionary<string, string> { { "header1", "header-value" } },
            KafkaCreateConsumerScopeEnabled = false,
            LogsInjectionEnabled = true,
            MaxTracesSubmittedPerSecond = 50,
            ServiceName = "my-test-service",
            ServiceVersion = "1.2.3",
            StartupDiagnosticLogEnabled = false,
            StatsComputationEnabled = true,
            TraceEnabled = false,
            TracerMetricsEnabled = true,
        };

        manual.Integrations[nameof(IntegrationId.Aerospike)].Enabled = false;
        manual.Integrations[nameof(IntegrationId.Grpc)].AnalyticsEnabled = true;
        manual.Integrations[nameof(IntegrationId.Couchbase)].AnalyticsSampleRate = 0.5;

        var mappings = new Dictionary<string, string> { { "some-service", "some-mapping" } };
        int[] clientErrors = [400, 401, 402];
        int[] serverErrors = [500, 501, 502];

        manual.SetServiceNameMappings(mappings);
        manual.SetHttpClientErrorStatusCodes(clientErrors);
        manual.SetHttpServerErrorStatusCodes(serverErrors);

        var changedValues = manual.ToDictionary();

        IConfigurationSource configSource = useLegacySettings
                               ? new ManualInstrumentationLegacyConfigurationSource(changedValues, useDefaultSources: false)
                               : new ManualInstrumentationConfigurationSource(changedValues, useDefaultSources: false);
        var automatic = new TracerSettings(configSource);

        AssertEquivalent(manual, automatic);
        automatic.Manager.InitialMutableSettings.ServiceNameMappings.Should().Equal(mappings);
        automatic.Manager.InitialMutableSettings.HttpClientErrorStatusCodes.Should().Equal(MutableSettings.ParseHttpCodesToArray(string.Join(",", clientErrors)));
        automatic.Manager.InitialMutableSettings.HttpServerErrorStatusCodes.Should().Equal(MutableSettings.ParseHttpCodesToArray(string.Join(",", serverErrors)));

        automatic.Manager.InitialMutableSettings.Integrations[IntegrationId.OpenTelemetry].Enabled.Should().BeFalse();
        automatic.Manager.InitialMutableSettings.Integrations[IntegrationId.Kafka].Enabled.Should().BeFalse();
        automatic.Manager.InitialMutableSettings.Integrations[IntegrationId.Aerospike].Enabled.Should().BeFalse();
        automatic.Manager.InitialMutableSettings.Integrations[IntegrationId.Grpc].AnalyticsEnabled.Should().BeTrue();
        automatic.Manager.InitialMutableSettings.Integrations[IntegrationId.Couchbase].AnalyticsSampleRate.Should().Be(0.5);
    }

    [Fact]
    public void AutomaticToManual_ImmutableSettingsAreTransferredCorrectly()
    {
        var automatic = GetAndAssertAutomaticTracerSettings();

        Dictionary<string, object> serializedSettings = new();
        CtorIntegration.PopulateSettings(serializedSettings, automatic);

        var manual = new ImmutableManualSettings(serializedSettings);

        manual.AgentUri.Should().Be(automatic.Exporter.AgentUri);
        manual.Exporter.AgentUri.Should().Be(automatic.Exporter.AgentUri);
        manual.AnalyticsEnabled.Should().Be(automatic.Manager.InitialMutableSettings.AnalyticsEnabled);
        manual.CustomSamplingRules.Should().Be(automatic.Manager.InitialMutableSettings.CustomSamplingRules);
        manual.Environment.Should().Be(automatic.Manager.InitialMutableSettings.Environment);
        manual.GlobalSamplingRate.Should().Be(automatic.Manager.InitialMutableSettings.GlobalSamplingRate);
        manual.GlobalTags.Should().BeEquivalentTo(automatic.Manager.InitialMutableSettings.GlobalTags);
        manual.HeaderTags.Should().BeEquivalentTo(automatic.Manager.InitialMutableSettings.HeaderTags);
        // force fluent assertions to just compare the properties, not use the `Equals` implementation
        manual.Integrations.Settings.Should()
              .BeEquivalentTo(
                   automatic.Manager.InitialMutableSettings.Integrations.Settings.ToDictionary(x => x.IntegrationName, x => x),
                   options => options.ComparingByMembers(typeof(IntegrationSettings)));
        manual.KafkaCreateConsumerScopeEnabled.Should().Be(automatic.Manager.InitialMutableSettings.KafkaCreateConsumerScopeEnabled);
        manual.LogsInjectionEnabled.Should().Be(automatic.Manager.InitialMutableSettings.LogsInjectionEnabled);
        manual.MaxTracesSubmittedPerSecond.Should().Be(automatic.Manager.InitialMutableSettings.MaxTracesSubmittedPerSecond);
        manual.ServiceName.Should().Be(automatic.Manager.InitialMutableSettings.ServiceName);
        manual.ServiceVersion.Should().Be(automatic.Manager.InitialMutableSettings.ServiceVersion);
        manual.StartupDiagnosticLogEnabled.Should().Be(automatic.Manager.InitialMutableSettings.StartupDiagnosticLogEnabled);
        manual.StatsComputationEnabled.Should().Be(automatic.StatsComputationEnabled);
        manual.TraceEnabled.Should().Be(automatic.Manager.InitialMutableSettings.TraceEnabled);
        manual.TracerMetricsEnabled.Should().Be(automatic.Manager.InitialMutableSettings.TracerMetricsEnabled);

        manual.Integrations[nameof(IntegrationId.OpenTelemetry)].Enabled.Should().BeFalse();
        manual.Integrations[nameof(IntegrationId.Kafka)].Enabled.Should().BeFalse();
        manual.Integrations[nameof(IntegrationId.Aerospike)].Enabled.Should().BeFalse();
        manual.Integrations[nameof(IntegrationId.Grpc)].AnalyticsEnabled.Should().BeTrue();
        manual.Integrations[nameof(IntegrationId.Couchbase)].AnalyticsSampleRate.Should().Be(0.5);
    }

    private static void AssertEquivalent(ManualSettings manual, TracerSettings automatic)
    {
        // AgentUri gets transformed in exporter settings, so hacking around that here
        GetTransformedAgentUri(manual.AgentUri).Should().Be(automatic.Exporter.AgentUri);
        GetTransformedAgentUri(manual.Exporter.AgentUri).Should().Be(automatic.Exporter.AgentUri);

        manual.AnalyticsEnabled.Should().Be(automatic.Manager.InitialMutableSettings.AnalyticsEnabled);
        manual.CustomSamplingRules.Should().Be(automatic.Manager.InitialMutableSettings.CustomSamplingRules);
        manual.DiagnosticSourceEnabled.Should().Be(GlobalSettings.Instance.DiagnosticSourceEnabled);
        manual.Environment.Should().Be(automatic.Manager.InitialMutableSettings.Environment);
        manual.GlobalSamplingRate.Should().Be(automatic.Manager.InitialMutableSettings.GlobalSamplingRate);
        manual.GlobalTags.Should().BeEquivalentTo(automatic.Manager.InitialMutableSettings.GlobalTags);
        // These _aren't_ necessarily equivalent because of DisabledIntegrations.
        // If you add an integration to the manual.DisabledIntegrations, then the automatic.Integrations
        // will include it, but the original manual.Integrations _won't_. All a bit of a mess, but
        // essentially due to the legacy design of the TracerSettings object
        // manual.Integrations.Settings.Should().BeEquivalentTo(automatic.Integrations.Settings.ToDictionary(x => x.IntegrationName, x => x));
        manual.KafkaCreateConsumerScopeEnabled.Should().Be(automatic.Manager.InitialMutableSettings.KafkaCreateConsumerScopeEnabled);
        manual.LogsInjectionEnabled.Should().Be(automatic.Manager.InitialMutableSettings.LogsInjectionEnabled);
        manual.MaxTracesSubmittedPerSecond.Should().Be(automatic.Manager.InitialMutableSettings.MaxTracesSubmittedPerSecond);
        manual.ServiceName.Should().Be(automatic.Manager.InitialMutableSettings.ServiceName);
        manual.ServiceVersion.Should().Be(automatic.Manager.InitialMutableSettings.ServiceVersion);
        manual.StartupDiagnosticLogEnabled.Should().Be(automatic.Manager.InitialMutableSettings.StartupDiagnosticLogEnabled);
        manual.StatsComputationEnabled.Should().Be(automatic.StatsComputationEnabled);
        manual.TraceEnabled.Should().Be(automatic.Manager.InitialMutableSettings.TraceEnabled);
        manual.TracerMetricsEnabled.Should().Be(automatic.Manager.InitialMutableSettings.TracerMetricsEnabled);

        Uri GetTransformedAgentUri(Uri agentUri)
            => ExporterSettings.Create(new()
            {
                { ConfigurationKeys.AgentUri, agentUri }
            }).AgentUri;
    }

    private static string[] GetAllTracerSettingKeys()
    {
        var keyFields = typeof(TracerSettingKeyConstants).GetFields();
        // should only have consts in this class
        keyFields.Should().OnlyContain(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string), "TracerSettingKeyConstants should only contain string constants");

        var keys = keyFields.Select(x => (string)x.GetRawConstantValue()).ToArray();
        keys.Should().NotBeEmpty().And.OnlyContain(x => !string.IsNullOrEmpty(x), "TracerSettingKeyConstants keys should not be null or empty");

        return keys;
    }

    private static string[] GetAutomaticTracerSettingKeys()
        => GetAllTracerSettingKeys()
          .Where(
               x => x is not "IsFromDefaultSources"
                        and not "DD_TRACE_HTTP_CLIENT_ERROR_STATUSES"
                        and not "DD_TRACE_HTTP_SERVER_ERROR_STATUSES"
                        and not "DD_TRACE_SERVICE_MAPPING")
          .ToArray();

    private static string[] GetManualTracerSettingKeys()
        => GetAllTracerSettingKeys()
          .Where(x => x is not "DD_DIAGNOSTIC_SOURCE_ENABLED")
          .ToArray();

    private static TracerSettings GetAndAssertAutomaticTracerSettings()
    {
        var automatic = TracerSettings.Create(new()
        {
            { ConfigurationKeys.GlobalAnalyticsEnabled, true },
            { ConfigurationKeys.CustomSamplingRules, """[{"sample_rate":0.3, "service":"shopping-cart.*"}]""" },
            { ConfigurationKeys.DiagnosticSourceEnabled, true },
            { ConfigurationKeys.DisabledIntegrations, $"something;OpenTelemetry;{nameof(IntegrationId.Kafka)}" },
            { ConfigurationKeys.Environment, "my-test-env" },
            { ConfigurationKeys.GlobalSamplingRate, 0.5 },
            { ConfigurationKeys.GlobalTags, "tag1:value" },
            { ConfigurationKeys.GrpcTags, "grpc1:grpc-value" },
            { ConfigurationKeys.HeaderTags, "header1:header-value" },
            { ConfigurationKeys.KafkaCreateConsumerScopeEnabled, false },
            { ConfigurationKeys.LogsInjectionEnabled, true },
            { ConfigurationKeys.MaxTracesSubmittedPerSecond, 50 },
            { ConfigurationKeys.ServiceName, "my-test-service" },
            { ConfigurationKeys.ServiceVersion, "1.2.3" },
            { ConfigurationKeys.StartupDiagnosticLogEnabled, false }, // can't actually be changed
            { ConfigurationKeys.StatsComputationEnabled, true },
            { ConfigurationKeys.TraceEnabled, false },
            { ConfigurationKeys.TracerMetricsEnabled, true },
            { ConfigurationKeys.AgentUri, "http://localhost:1234" },
            { string.Format(IntegrationSettings.IntegrationEnabled, nameof(IntegrationId.Aerospike)), "false" },
            { string.Format(IntegrationSettings.AnalyticsEnabledKey, nameof(IntegrationId.Grpc)), "true" },
            { string.Format(IntegrationSettings.AnalyticsSampleRateKey, nameof(IntegrationId.Couchbase)), 0.5 },
        });

        // verify that all the settings are as expected
        automatic.Manager.InitialMutableSettings.AnalyticsEnabled.Should().Be(true);
        automatic.Manager.InitialMutableSettings.CustomSamplingRules.Should().Be("""[{"sample_rate":0.3, "service":"shopping-cart.*"}]""");
        GlobalSettings.Instance.DiagnosticSourceEnabled.Should().Be(true);
        automatic.Manager.InitialMutableSettings.DisabledIntegrationNames.Should().BeEquivalentTo(["something", "OpenTelemetry", "Kafka"]);
        automatic.Manager.InitialMutableSettings.Environment.Should().Be("my-test-env");
        automatic.Manager.InitialMutableSettings.GlobalSamplingRate.Should().Be(0.5);
        automatic.Manager.InitialMutableSettings.GlobalTags.Should().BeEquivalentTo(new Dictionary<string, string> { { "tag1", "value" } });
        automatic.Manager.InitialMutableSettings.GrpcTags.Should().BeEquivalentTo(new Dictionary<string, string> { { "grpc1", "grpc-value" } });
        automatic.Manager.InitialMutableSettings.HeaderTags.Should().BeEquivalentTo(new Dictionary<string, string> { { "header1", "header-value" } });
        automatic.Manager.InitialMutableSettings.KafkaCreateConsumerScopeEnabled.Should().Be(false);
        automatic.Manager.InitialMutableSettings.LogsInjectionEnabled.Should().Be(true);
        automatic.Manager.InitialMutableSettings.MaxTracesSubmittedPerSecond.Should().Be(50);
        automatic.Manager.InitialMutableSettings.ServiceName.Should().Be("my-test-service");
        automatic.Manager.InitialMutableSettings.ServiceVersion.Should().Be("1.2.3");
        automatic.Manager.InitialMutableSettings.StartupDiagnosticLogEnabled.Should().Be(false);
        automatic.StatsComputationEnabled.Should().Be(true);
        automatic.Manager.InitialMutableSettings.TraceEnabled.Should().Be(false);
        automatic.Manager.InitialMutableSettings.TracerMetricsEnabled.Should().Be(true);
        automatic.Exporter.AgentUri.Should().Be(new Uri("http://127.0.0.1:1234"));
        automatic.Manager.InitialMutableSettings.Integrations[nameof(IntegrationId.Aerospike)].Enabled.Should().Be(false);
        automatic.Manager.InitialMutableSettings.Integrations[nameof(IntegrationId.Grpc)].AnalyticsEnabled.Should().Be(true);
        automatic.Manager.InitialMutableSettings.Integrations[nameof(IntegrationId.Couchbase)].AnalyticsSampleRate.Should().Be(0.5);
        automatic.Manager.InitialMutableSettings.Integrations[nameof(IntegrationId.Kafka)].Enabled.Should().BeFalse();

        return automatic;
    }
}
