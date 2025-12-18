// <copyright file="ManualInstrumentationConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration.ConfigurationSources;

/// <summary>
/// Wraps the settings passed in from the manual instrumentation API in a configuration source, to make it easier to integrate
/// </summary>
internal sealed class ManualInstrumentationConfigurationSource : ManualInstrumentationConfigurationSourceBase
{
    public ManualInstrumentationConfigurationSource(IReadOnlyDictionary<string, object?> dictionary, bool useDefaultSources)
        : base(dictionary, useDefaultSources)
    {
    }

    protected override bool TryGetValue(string key, out object? value)
    {
        // Get the value for the given key, but also record telemetry
        // This is also where any "remapping" should be done, in cases
        // where either the manual-instrumentation key differs or the
        // type stored in the dictionary differs

        var result = base.TryGetValue(key, out value);
        if (result)
        {
            if (GetTelemetryKey(key) is { } telemetryKey)
            {
                TelemetryFactory.Metrics.Record(telemetryKey);
            }

            if (value is not null)
            {
                value = RemapResult(key, value);
            }
        }

        return result;
    }

    internal static PublicApiUsage? GetTelemetryKey(string key) => key switch
    {
        TracerSettingKeyConstants.AgentUriKey => PublicApiUsage.ExporterSettings_AgentUri_Set,
        TracerSettingKeyConstants.AnalyticsEnabledKey => PublicApiUsage.TracerSettings_AnalyticsEnabled_Set,
        TracerSettingKeyConstants.CustomSamplingRules => PublicApiUsage.TracerSettings_CustomSamplingRules_Set,
        TracerSettingKeyConstants.DiagnosticSourceEnabledKey => PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set,
        TracerSettingKeyConstants.DisabledIntegrationNamesKey => PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set,
        TracerSettingKeyConstants.EnvironmentKey => PublicApiUsage.TracerSettings_Environment_Set,
        TracerSettingKeyConstants.GlobalSamplingRateKey => PublicApiUsage.TracerSettings_GlobalSamplingRate_Set,
        TracerSettingKeyConstants.GrpcTags => PublicApiUsage.TracerSettings_GrpcTags_Set,
        TracerSettingKeyConstants.HeaderTags => PublicApiUsage.TracerSettings_HeaderTags_Set,
        TracerSettingKeyConstants.GlobalTagsKey => PublicApiUsage.TracerSettings_GlobalTags_Set,
        TracerSettingKeyConstants.HttpClientErrorCodesKey => PublicApiUsage.TracerSettings_SetHttpClientErrorStatusCodes,
        TracerSettingKeyConstants.HttpServerErrorCodesKey => PublicApiUsage.TracerSettings_SetHttpServerErrorStatusCodes,
        TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey => PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set,
        TracerSettingKeyConstants.LogsInjectionEnabledKey => PublicApiUsage.TracerSettings_LogsInjectionEnabled_Set,
        TracerSettingKeyConstants.ServiceNameKey => PublicApiUsage.TracerSettings_ServiceName_Set,
        TracerSettingKeyConstants.ServiceNameMappingsKey => PublicApiUsage.TracerSettings_SetServiceNameMappings,
        TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey => PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set,
        TracerSettingKeyConstants.ServiceVersionKey => PublicApiUsage.TracerSettings_ServiceVersion_Set,
        TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey => PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set,
        TracerSettingKeyConstants.StatsComputationEnabledKey => PublicApiUsage.TracerSettings_StatsComputationEnabled_Set,
        TracerSettingKeyConstants.TraceEnabledKey => PublicApiUsage.TracerSettings_TraceEnabled_Set,
        TracerSettingKeyConstants.TracerMetricsEnabledKey => PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set,
        TracerSettingKeyConstants.IntegrationSettingsKey => PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set,
        // These are pretty hacky but about the best we can do
        _ when key.EndsWith("_ANALYTICS_ENABLED", StringComparison.Ordinal) => PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set,
        _ when key.EndsWith("_ANALYTICS_SAMPLE_RATE", StringComparison.Ordinal) => PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Set,
        _ when key.StartsWith("DD_TRACE_", StringComparison.Ordinal) && key.EndsWith("_ANALYTICS_SAMPLE_RATE", StringComparison.Ordinal)
            => PublicApiUsage.IntegrationSettings_Enabled_Set, // this could definitely be too broad, but about the best we can reasonably do
        _ => null
    };

    private static object RemapResult(string key, object value) => key switch
    {
        TracerSettingKeyConstants.AgentUriKey => value is Uri uri ? uri.ToString() : value,
        TracerSettingKeyConstants.DisabledIntegrationNamesKey => value is HashSet<string> set ? string.Join(";", set) : value,
        TracerSettingKeyConstants.HttpServerErrorCodesKey => value is List<int> list
                                                                 ? string.Join(",", list.Select(i => i.ToString(CultureInfo.InvariantCulture)))
                                                                 : value,
        TracerSettingKeyConstants.HttpClientErrorCodesKey => value is List<int> list
                                                                 ? string.Join(",", list.Select(i => i.ToString(CultureInfo.InvariantCulture)))
                                                                 : value,
        _ => value,
    };
}
