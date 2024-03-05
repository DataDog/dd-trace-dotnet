// <copyright file="SettingsInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.TracerSettingKeyConstants;
using ImmutableManualSettings = DatadogTraceManual::Datadog.Trace.Configuration.ImmutableTracerSettings;
using ManualITracerSettings = DatadogTraceManual::Datadog.Trace.Configuration.ITracerSettings;
using ManualSettings = DatadogTraceManual::Datadog.Trace.Configuration.TracerSettings;
using NullTracerSettings = DatadogTraceManual::Datadog.Trace.Configuration.NullTracerSettings;

namespace Datadog.Trace.Tests.ManualInstrumentation;

#pragma warning disable CS0618 // Type or member is obsolete
public class SettingsInstrumentationTests
{
    [Fact]
    public void AutomaticToManual_AllDefaultSettingsAreTransferredCorrectly()
    {
        var automatic = new TracerSettings();
        var proxy = automatic.DuckCast<ManualITracerSettings>();

        var manual = new ManualSettings(proxy, isFromDefaultSources: false);

        AssertEquivalent(manual, automatic);
    }

    [Fact]
    public void AutomaticToManual_IncludesAllExpectedKeys()
    {
        var automatic = new TracerSettings();
        var proxy = automatic.DuckCast<ManualITracerSettings>();

        // ensure that we return a value for all the expected keys
        var keys = GetAutomaticTracerSettingKeys();
        using var scope = new AssertionScope();
        foreach (var key in keys)
        {
            AssertHasExpectedKey(key, proxy);
        }
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

        var manual = new ManualSettings(automatic.DuckCast<ManualITracerSettings>(), isFromDefaultSources: false);

        AssertEquivalent(manual, automatic);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ManualToAutomatic_IncludesNoKeysWhenNotChanged(bool isFromDefaultsExpected)
    {
        var manual = new ManualSettings(NullTracerSettings.Instance, isFromDefaultSources: isFromDefaultsExpected);
        var proxy = manual.DuckCast<ManualITracerSettings>();

        // should always have this key
        proxy.TryGetBool(BoolKeys.IsFromDefaultSourcesKey, out var isFromDefaults).Should().BeTrue();
        isFromDefaults.Should().Be(isFromDefaultsExpected);

        var allKeys = GetAllTracerSettingKeys()
                     .Where(x => x.Key != BoolKeys.IsFromDefaultSourcesKey);

        using var s = new AssertionScope();
        foreach (var (name, key) in allKeys)
        {
            // these are only applicable in one slot, but test them all to be certain
            proxy.TryGetObject(key, out _).Should().BeFalse();
            proxy.TryGetBool(key, out _).Should().BeFalse();
            proxy.TryGetInt(key, out _).Should().BeFalse();
            proxy.TryGetDouble(key, out _).Should().BeFalse();
            proxy.TryGetNullableBool(key, out _).Should().BeFalse();
            proxy.TryGetNullableInt(key, out _).Should().BeFalse();
            proxy.TryGetNullableDouble(key, out _).Should().BeFalse();
        }
    }

    [Fact]
    public void ManualToAutomatic_IncludesAllExpectedKeys()
    {
        // change all the defaults to make sure we add the keys to the dictionary
        var originalSettings = new TracerSettings().DuckCast<ManualITracerSettings>();
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

        var proxy = manual.DuckCast<ManualITracerSettings>();

        foreach (var key in GetManualTracerSettingKeys())
        {
            AssertHasExpectedKey(key, proxy);
        }
    }

    [Fact]
    public void ManualToAutomatic_CustomSettingsAreTransferredCorrectly()
    {
        var initialValues = new TracerSettings().DuckCast<ManualITracerSettings>();
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

        var proxy = manual.DuckCast<ITracerSettings>();

        var automatic = new TracerSettings();
        ConfigureIntegration.UpdateSettings(in proxy, automatic);

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

        var serializedSettings = immutable.DuckCast<ManualITracerSettings>();

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

    private static List<(string Name, string Key)> GetAllTracerSettingKeys()
    {
        var objectKeyFields = GetKeyFields(typeof(ObjectKeys), nameof(ObjectKeys));
        var boolKeyFields = GetKeyFields(typeof(BoolKeys), nameof(BoolKeys));
        var intKeyFields = GetKeyFields(typeof(IntKeys), nameof(IntKeys));
        var doubleKeyFields = GetKeyFields(typeof(NullableDoubleKeys), nameof(NullableDoubleKeys));

        return objectKeyFields
              .Concat(boolKeyFields)
              .Concat(intKeyFields)
              .Concat(doubleKeyFields)
              .ToList();

        static IEnumerable<(string Name, string Key)> GetKeyFields(Type type, string name)
        {
            var keyFields = type.GetFields();
            // should only have consts in this class
            keyFields.Should().OnlyContain(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string), $"{name} should only contain string constants");

            var keys = keyFields.Select(x => (string)x.GetRawConstantValue()).ToArray();
            keys.Should().NotBeEmpty().And.OnlyContain(x => !string.IsNullOrEmpty(x), $"{name} keys should not be null or empty");

            return keys.Select(x => (name, x));
        }
    }

    private static IEnumerable<(string Name, string Key)> GetAutomaticTracerSettingKeys()
        => GetAllTracerSettingKeys()
           .Where(
                x => x.Key is not "IsFromDefaultSources"
                         and not "DD_HTTP_CLIENT_ERROR_STATUSES"
                         and not "DD_HTTP_SERVER_ERROR_STATUSES"
                         and not "DD_TRACE_SERVICE_MAPPING");

    private static IEnumerable<(string Name, string Key)> GetManualTracerSettingKeys()
        => GetAllTracerSettingKeys()
           .Where(x => x.Key is not "DD_DIAGNOSTIC_SOURCE_ENABLED");

    private static void AssertHasExpectedKey<T>((string Name, string Key) key, T proxy)
        where T : ManualITracerSettings
    {
        (key.Name switch
                {
                    nameof(ObjectKeys) => proxy.TryGetObject(key.Key, out _),
                    nameof(BoolKeys) => proxy.TryGetBool(key.Key, out _),
                    nameof(IntKeys) => proxy.TryGetInt(key.Key, out _),
                    nameof(NullableDoubleKeys) => proxy.TryGetNullableDouble(key.Key, out _),
                    _ => throw new InvalidOperationException("Unexpected key type:" + key.Name),
                }).Should()
                  .BeTrue($"TracerSettings should expect the key {key.Key} ({key.Name}");
    }
}
