// <copyright file="TracerSettingsSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Holds a snapshot of the mutable <see cref="TracerSettings"/> values after
/// initial creation. Used for tracking which properties have been changed in code
/// subsequently. Only properties which can be modified externally (both publicly, and
/// outside the mutable settings objects should be recorded. Mutation of settings
/// _inside_ <see cref="TracerSettings"/> et al should be explicitly tracked when
/// they are mutated.
/// </summary>
internal class TracerSettingsSnapshot
{
    private static readonly HashSet<string> EmptyHashSet = new();

    public TracerSettingsSnapshot(TracerSettings settings)
    {
        Environment = settings.EnvironmentInternal;
        ServiceName = settings.ServiceNameInternal;
        ServiceVersion = settings.ServiceVersionInternal;
        TraceEnabled = settings.TraceEnabledInternal;
        Exporter = new ExporterSettingsSnapshot(settings.ExporterInternal);
#pragma warning disable CS0618 // obsolete
        AnalyticsEnabled = settings.AnalyticsEnabledInternal;
#pragma warning restore CS0618
        MaxTracesSubmittedPerSecond = settings.MaxTracesSubmittedPerSecondInternal;
        CustomSamplingRules = settings.CustomSamplingRulesInternal;
        LogsInjectionEnabled = settings.LogSubmissionSettings.LogsInjectionEnabled;
        LogsInjectionEnabled = settings.LogSubmissionSettings.LogsInjectionEnabled;
        GlobalSamplingRate = settings.GlobalSamplingRateInternal;
        DisabledIntegrationNames = GetHashSet(settings.DisabledIntegrationNamesInternal);
        // this is the easiest way to create a "snapshot" of the settings
        Integrations = new ImmutableIntegrationSettingsCollection(settings.IntegrationsInternal, EmptyHashSet);
        GlobalTags = GetDictionary(settings.GlobalTagsInternal);
        HeaderTags = GetDictionary(settings.HeaderTagsInternal);
        GrpcTags = GetDictionary(settings.GrpcTagsInternal);
        TracerMetricsEnabled = settings.TracerMetricsEnabledInternal;
        StatsComputationEnabled = settings.StatsComputationEnabledInternal;
        KafkaCreateConsumerScopeEnabled = settings.KafkaCreateConsumerScopeEnabledInternal;
        StartupDiagnosticLogEnabled = settings.StartupDiagnosticLogEnabledInternal;
        DirectLogSubmissionBatchPeriod = settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod;
    }

    public string? Environment { get; }

    public string? ServiceName { get; }

    public string? ServiceVersion { get; }

    public bool TraceEnabled { get; }

    public ExporterSettingsSnapshot Exporter { get; }

    public bool AnalyticsEnabled { get; }

    public int MaxTracesSubmittedPerSecond { get; }

    public string? CustomSamplingRules { get; }

    public bool? LogsInjectionEnabled { get; }

    public double? GlobalSamplingRate { get; }

    public HashSet<string>? DisabledIntegrationNames { get; }

    public ImmutableIntegrationSettingsCollection Integrations { get; }

    public IDictionary<string, string>? GlobalTags { get; }

    public IDictionary<string, string>? HeaderTags { get; }

    public IDictionary<string, string>? GrpcTags { get; }

    public bool TracerMetricsEnabled { get; }

    public bool StatsComputationEnabled { get; }

    public bool KafkaCreateConsumerScopeEnabled { get; }

    public bool StartupDiagnosticLogEnabled { get; }

    public TimeSpan DirectLogSubmissionBatchPeriod { get; }

    private static Dictionary<string, string>? GetDictionary(IDictionary<string, string>? source)
    {
        if (source is null or { Count: 0 })
        {
            return null;
        }

        return new Dictionary<string, string>(source);
    }

    private static HashSet<string>? GetHashSet(HashSet<string>? source)
    {
        if (source is null or { Count: 0 })
        {
            return null;
        }

        return new HashSet<string>(source);
    }

    /// <summary>
    /// Record any changes to the provided <see cref="TracerSettings"/> in <paramref name="telemetry"/>
    /// </summary>
    public void RecordChanges(TracerSettings settings, IConfigurationTelemetry telemetry)
    {
        RecordIfChanged(telemetry, ConfigurationKeys.Environment, Environment, settings.EnvironmentInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.ServiceName, ServiceName, settings.ServiceNameInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.ServiceVersion, ServiceVersion, settings.ServiceVersionInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.TraceEnabled, TraceEnabled, settings.TraceEnabledInternal);
#pragma warning disable CS0618
        RecordIfChanged(telemetry, ConfigurationKeys.GlobalAnalyticsEnabled, AnalyticsEnabled, settings.AnalyticsEnabledInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.MaxTracesSubmittedPerSecond, MaxTracesSubmittedPerSecond, settings.MaxTracesSubmittedPerSecondInternal);
#pragma warning restore CS0618
        RecordIfChanged(telemetry, ConfigurationKeys.CustomSamplingRules, CustomSamplingRules, settings.CustomSamplingRulesInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.LogsInjectionEnabled, LogsInjectionEnabled, settings.LogSubmissionSettings.LogsInjectionEnabled);
        RecordIfChanged(telemetry, ConfigurationKeys.GlobalSamplingRate, GlobalSamplingRate, settings.GlobalSamplingRateInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.DisabledIntegrations, DisabledIntegrationNames, settings.DisabledIntegrationNamesInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.GlobalTags, GlobalTags, GetDictionary(settings.GlobalTagsInternal));
        RecordIfChanged(telemetry, ConfigurationKeys.HeaderTags, HeaderTags, GetDictionary(settings.HeaderTagsInternal));
        RecordIfChanged(telemetry, ConfigurationKeys.GrpcTags, GrpcTags, GetDictionary(settings.GrpcTagsInternal));
        RecordIfChanged(telemetry, ConfigurationKeys.TracerMetricsEnabled, TracerMetricsEnabled, settings.TracerMetricsEnabledInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.StatsComputationEnabled, StatsComputationEnabled, settings.StatsComputationEnabledInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.KafkaCreateConsumerScopeEnabled, KafkaCreateConsumerScopeEnabled, settings.KafkaCreateConsumerScopeEnabledInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.StartupDiagnosticLogEnabled, StartupDiagnosticLogEnabled, settings.StartupDiagnosticLogEnabledInternal);
        RecordIfChanged(telemetry, ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, (int)DirectLogSubmissionBatchPeriod.TotalSeconds, (int)settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod.TotalSeconds);

        for (var i = settings.IntegrationsInternal.Settings.Length - 1; i >= 0; i--)
        {
            var newValue = settings.IntegrationsInternal.Settings[i];
            var oldValue = Integrations.Settings[i];
            var integrationName = newValue.IntegrationNameInternal;
            RecordIfChanged(telemetry, string.Format(ConfigurationKeys.Integrations.Enabled, integrationName), oldValue.EnabledInternal, newValue.EnabledInternal);
#pragma warning disable 618 // App analytics is deprecated, but still used
            RecordIfChanged(telemetry, string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName), oldValue.AnalyticsEnabledInternal, newValue.AnalyticsEnabledInternal);
            RecordIfChanged(telemetry, string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName), oldValue.AnalyticsSampleRateInternal, newValue.AnalyticsSampleRateInternal);
#pragma warning restore 618
        }

        Exporter.RecordChanges(settings.ExporterInternal, telemetry);
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            telemetry.Record(key, newValue, recordValue: true, ConfigurationOrigins.Code);
        }
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, int? oldValue, int? newValue)
    {
        if (oldValue != newValue)
        {
            telemetry.Record(key, newValue, ConfigurationOrigins.Code);
        }
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, double? oldValue, double? newValue)
    {
        if (oldValue != newValue)
        {
            telemetry.Record(key, newValue, ConfigurationOrigins.Code);
        }
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            telemetry.Record(key, newValue, ConfigurationOrigins.Code);
        }
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, bool? oldValue, bool? newValue)
    {
        if (oldValue != newValue)
        {
            if (newValue is null)
            {
                telemetry.Record(key, null, recordValue: true, ConfigurationOrigins.Code);
            }
            else
            {
                telemetry.Record(key, newValue.Value, ConfigurationOrigins.Code);
            }
        }
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, IDictionary<string, string>? oldValues, IDictionary<string, string>? newValues)
    {
        if (oldValues is null
         && (newValues is null || newValues is { Count: 0 }))
        {
            return;
        }

        var equal = oldValues is not null
                 && newValues is not null
                 && oldValues.Count == newValues.Count;

        if (equal)
        {
            foreach (var kvp in oldValues!)
            {
                if (!newValues!.TryGetValue(kvp.Key, out var newValue)
                    || !string.Equals(kvp.Value, newValue, StringComparison.Ordinal))
                {
                    equal = false;
                    break;
                }
            }
        }

        if (equal)
        {
            return;
        }

        var telemetryValue = newValues switch
        {
            null => null,
            { Count: 0 } => string.Empty,
            _ => string.Join(",", newValues.Select(x => $"{x.Key}:{x.Value}")),
        };

        telemetry.Record(key, telemetryValue, recordValue: true, ConfigurationOrigins.Code);
    }

    private static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, HashSet<string>? oldValues, HashSet<string>? newValues)
    {
        if (oldValues is null
         && (newValues is null || newValues is { Count: 0 }))
        {
            return;
        }

        var equal = oldValues is not null
                 && newValues is not null
                 && oldValues.Count == newValues.Count;

        if (equal)
        {
            foreach (var oldValue in oldValues!)
            {
                if (!newValues!.Contains(oldValue))
                {
                    equal = false;
                    break;
                }
            }
        }

        if (equal)
        {
            return;
        }

        var telemetryValue = newValues switch
        {
            null => null,
            { Count: 0 } => string.Empty,
            _ => string.Join(",", newValues),
        };

        telemetry.Record(key, telemetryValue, recordValue: true, ConfigurationOrigins.Code);
    }

    public class ExporterSettingsSnapshot
    {
        public ExporterSettingsSnapshot(ExporterSettings settings)
        {
            AgentUri = settings.AgentUriInternal;
            TracesPipeName = settings.TracesPipeNameInternal;
            TracesPipeTimeoutMs = settings.TracesPipeTimeoutMsInternal;
            MetricsPipeName = settings.MetricsPipeNameInternal;
            DogStatsdPort = settings.DogStatsdPortInternal;
            PartialFlushEnabled = settings.PartialFlushEnabledInternal;
            PartialFlushMinSpans = settings.PartialFlushMinSpansInternal;
            TracesUnixDomainSocketPath = settings.TracesUnixDomainSocketPathInternal;
            MetricsUnixDomainSocketPath = settings.MetricsUnixDomainSocketPathInternal;
        }

        public Uri AgentUri { get; }

        public string? TracesPipeName { get; }

        public int TracesPipeTimeoutMs { get; }

        public string? MetricsPipeName { get; }

        public int DogStatsdPort { get; }

        public bool PartialFlushEnabled { get; }

        public int PartialFlushMinSpans { get; }

        public string? TracesUnixDomainSocketPath { get; }

        public string? MetricsUnixDomainSocketPath { get; }

        public void RecordChanges(ExporterSettings settings, IConfigurationTelemetry telemetry)
        {
            RecordIfChanged(telemetry, ConfigurationKeys.AgentUri, AgentUri.ToString(), settings.AgentUriInternal.ToString());
            RecordIfChanged(telemetry, ConfigurationKeys.TracesPipeName, TracesPipeName, settings.TracesPipeNameInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.TracesPipeTimeoutMs, TracesPipeTimeoutMs, settings.TracesPipeTimeoutMsInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.MetricsPipeName, MetricsPipeName, settings.MetricsPipeNameInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.DogStatsdPort, DogStatsdPort, settings.DogStatsdPortInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.PartialFlushEnabled, PartialFlushEnabled, settings.PartialFlushEnabledInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.PartialFlushMinSpans, PartialFlushMinSpans, settings.PartialFlushMinSpansInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.TracesUnixDomainSocketPath, TracesUnixDomainSocketPath, settings.TracesUnixDomainSocketPathInternal);
            RecordIfChanged(telemetry, ConfigurationKeys.MetricsUnixDomainSocketPath, MetricsUnixDomainSocketPath, settings.MetricsUnixDomainSocketPathInternal);
        }
    }
}
