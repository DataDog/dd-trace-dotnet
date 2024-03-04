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
        var automatic = new TracerSettings
        {
            AnalyticsEnabled = true,
            CustomSamplingRules = """[{"sample_rate":0.3, "service":"shopping-cart.*"}]""",
            DiagnosticSourceEnabled = true,
            DisabledIntegrationNames = ["something"],
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

        automatic.Exporter.AgentUri = new Uri("http://localhost:1234");
        automatic.Integrations[nameof(IntegrationId.Aerospike)].Enabled = false;
        automatic.Integrations[nameof(IntegrationId.Grpc)].AnalyticsEnabled = true;
        automatic.Integrations[nameof(IntegrationId.Couchbase)].AnalyticsSampleRate = 0.5;

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
            DisabledIntegrationNames = ["something"],
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
        settings.Should().ContainKeys(keys).And.HaveSameCount(keys);
    }

    [Fact]
    public void ManualToAutomatic_CustomSettingsAreTransferredCorrectly()
    {
        Dictionary<string, object> initialValues = new();
        PopulateDictionaryIntegration.PopulateSettings(initialValues, new TracerSettings());

        var manual = new ManualSettings(initialValues, isFromDefaultSources: false)
        {
            AgentUri = new Uri("http://localhost:1234"),
            AnalyticsEnabled = true,
            CustomSamplingRules = """[{"sample_rate":0.3, "service":"shopping-cart.*"}]""",
            DiagnosticSourceEnabled = true,
            DisabledIntegrationNames = ["something"],
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

        var automatic = new TracerSettings();
        ConfigureIntegration.UpdateSettings(changedValues, automatic);

        AssertEquivalent(manual, automatic);
        automatic.ServiceNameMappings.Should().Equal(mappings);
        automatic.HttpClientErrorStatusCodes.Should().Equal(TracerSettings.ParseHttpCodesToArray(string.Join(",", clientErrors)));
        automatic.HttpServerErrorStatusCodes.Should().Equal(TracerSettings.ParseHttpCodesToArray(string.Join(",", serverErrors)));
    }

    [Fact]
    public void AutomaticToManual_ImmutableSettingsAreTransferredCorrectly()
    {
        var automatic = new TracerSettings
        {
            AnalyticsEnabled = true,
            CustomSamplingRules = """[{"sample_rate":0.3, "service":"shopping-cart.*"}]""",
            DiagnosticSourceEnabled = true,
            DisabledIntegrationNames = [nameof(IntegrationId.Kafka)],
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

        automatic.Exporter.AgentUri = new Uri("http://localhost:1234");
        automatic.Integrations[nameof(IntegrationId.Aerospike)].Enabled = false;
        automatic.Integrations[nameof(IntegrationId.Grpc)].AnalyticsEnabled = true;
        automatic.Integrations[nameof(IntegrationId.Couchbase)].AnalyticsSampleRate = 0.5;

        var immutable = automatic.Build();

        Dictionary<string, object> serializedSettings = new();
        CtorIntegration.PopulateSettings(serializedSettings, immutable);

        var manual = new ImmutableManualSettings(serializedSettings);

        manual.AgentUri.Should().Be(immutable.Exporter.AgentUri);
        manual.Exporter.AgentUri.Should().Be(immutable.Exporter.AgentUri);
        manual.AnalyticsEnabled.Should().Be(immutable.AnalyticsEnabled);
        manual.CustomSamplingRules.Should().Be(immutable.CustomSamplingRules);
        manual.Environment.Should().Be(immutable.Environment);
        manual.GlobalSamplingRate.Should().Be(immutable.GlobalSamplingRate);
        manual.GlobalTags.Should().BeEquivalentTo(immutable.GlobalTags);
        manual.HeaderTags.Should().BeEquivalentTo(immutable.HeaderTags);
        manual.Integrations.Settings.Should().BeEquivalentTo(immutable.Integrations.Settings.ToDictionary(x => x.IntegrationName, x => x));
        manual.KafkaCreateConsumerScopeEnabled.Should().Be(immutable.KafkaCreateConsumerScopeEnabled);
        manual.LogsInjectionEnabled.Should().Be(immutable.LogsInjectionEnabled);
        manual.MaxTracesSubmittedPerSecond.Should().Be(immutable.MaxTracesSubmittedPerSecond);
        manual.ServiceName.Should().Be(immutable.ServiceName);
        manual.ServiceVersion.Should().Be(immutable.ServiceVersion);
        manual.StartupDiagnosticLogEnabled.Should().Be(immutable.StartupDiagnosticLogEnabled);
        manual.StatsComputationEnabled.Should().Be(immutable.StatsComputationEnabled);
        manual.TraceEnabled.Should().Be(immutable.TraceEnabled);
        manual.TracerMetricsEnabled.Should().Be(immutable.TracerMetricsEnabled);
    }

    private static void AssertEquivalent(ManualSettings manual, TracerSettings automatic)
    {
        // AgentUri gets transformed in exporter settings, so hacking around that here
        GetTransformedAgentUri(manual.AgentUri).Should().Be(automatic.Exporter.AgentUri);
        GetTransformedAgentUri(manual.Exporter.AgentUri).Should().Be(automatic.Exporter.AgentUri);

        manual.AnalyticsEnabled.Should().Be(automatic.AnalyticsEnabled);
        manual.CustomSamplingRules.Should().Be(automatic.CustomSamplingRules);
        manual.DiagnosticSourceEnabled.Should().Be(automatic.DiagnosticSourceEnabled);
        manual.Environment.Should().Be(automatic.Environment);
        manual.GlobalSamplingRate.Should().Be(automatic.GlobalSamplingRate);
        manual.GlobalTags.Should().BeEquivalentTo(automatic.GlobalTags);
        manual.Integrations.Settings.Should().BeEquivalentTo(automatic.Integrations.Settings.ToDictionary(x => x.IntegrationName, x => x));
        manual.KafkaCreateConsumerScopeEnabled.Should().Be(automatic.KafkaCreateConsumerScopeEnabled);
        manual.LogsInjectionEnabled.Should().Be(automatic.LogsInjectionEnabled);
        manual.MaxTracesSubmittedPerSecond.Should().Be(automatic.MaxTracesSubmittedPerSecond);
        manual.ServiceName.Should().Be(automatic.ServiceName);
        manual.ServiceVersion.Should().Be(automatic.ServiceVersion);
        manual.StartupDiagnosticLogEnabled.Should().Be(automatic.StartupDiagnosticLogEnabled);
        manual.StatsComputationEnabled.Should().Be(automatic.StatsComputationEnabled);
        manual.TraceEnabled.Should().Be(automatic.TraceEnabled);
        manual.TracerMetricsEnabled.Should().Be(automatic.TracerMetricsEnabled);

        Uri GetTransformedAgentUri(Uri agentUri)
        {
            var e = new ExporterSettings();
            e.AgentUri = agentUri;
            return e.AgentUri;
        }
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
                        and not "DD_HTTP_CLIENT_ERROR_STATUSES"
                        and not "DD_HTTP_SERVER_ERROR_STATUSES"
                        and not "DD_TRACE_SERVICE_MAPPING")
          .ToArray();

    private static string[] GetManualTracerSettingKeys()
        => GetAllTracerSettingKeys()
          .Where(x => x is not "DD_DIAGNOSTIC_SOURCE_ENABLED")
          .ToArray();
}
