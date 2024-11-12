// <copyright file="ConfigureIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Void Datadog.Trace.Tracer::Configure(System.Collections.Generic.Dictionary`2[System.String,System.Object]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "Configure",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "System.Collections.Generic.Dictionary`2[System.String,System.Object]" },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ConfigureIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigureIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget>(Dictionary<string, object?> values)
    {
        // Is this from calling new TracerSettings() or TracerSettings.Global?
        var isFromDefaults = values.TryGetValue(TracerSettingKeyConstants.IsFromDefaultSourcesKey, out var value) && value is true;

        // Get the starting point
        var settings = isFromDefaults
                           ? TracerSettings.FromDefaultSourcesInternal()
                           : new TracerSettings(null, new ConfigurationTelemetry(), new OverrideErrorLog());

        // Update the settings based on the values they set
        UpdateSettings(values, settings);

        // Update the global instance
        Trace.Tracer.ConfigureInternal(new ImmutableTracerSettings(settings, true));

        return CallTargetState.GetDefault();
    }

    // Internal for testing
    internal static void UpdateSettings(Dictionary<string, object?> dictionary, TracerSettings tracerSettings)
    {
        foreach (var setting in dictionary)
        {
            switch (setting.Key)
            {
                case TracerSettingKeyConstants.AgentUriKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_AgentUri_Set);
                    tracerSettings.ExporterInternal.AgentUriInternal = (setting.Value as Uri)!;
                    break;

                case TracerSettingKeyConstants.AnalyticsEnabledKey:
#pragma warning disable CS0618 // Type or member is obsolete
                    TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set);
                    tracerSettings.AnalyticsEnabledInternal = (bool)setting.Value!;
#pragma warning restore CS0618 // Type or member is obsolete
                    break;

                case TracerSettingKeyConstants.CustomSamplingRules:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_CustomSamplingRules_Get);
                    tracerSettings.CustomSamplingRulesInternal = setting.Value as string;
                    break;

                case TracerSettingKeyConstants.DiagnosticSourceEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set);
                    // there is no setter, it doesn't do anything
                    break;

                case TracerSettingKeyConstants.DisabledIntegrationNamesKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set);
                    tracerSettings.DisabledIntegrationNamesInternal = setting.Value as HashSet<string> ?? [];
                    break;

                case TracerSettingKeyConstants.EnvironmentKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Environment_Set);
                    tracerSettings.EnvironmentInternal = setting.Value as string;
                    break;

                case TracerSettingKeyConstants.GlobalSamplingRateKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GlobalSamplingRate_Set);
                    tracerSettings.GlobalSamplingRateInternal = setting.Value as double?;
                    break;

                case TracerSettingKeyConstants.GrpcTags:
                    if (setting.Value is IDictionary<string, string> { } grpcTags)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GrpcTags_Set);
                        var currentTags = tracerSettings.GrpcTagsInternal;
                        // This is a replacement, so make sure to clear
                        // Could also use a setter
                        currentTags.Clear();
                        foreach (var tag in grpcTags)
                        {
                            currentTags[tag.Key] = tag.Value;
                        }
                    }

                    break;

                case TracerSettingKeyConstants.HeaderTags:
                    if (setting.Value is IDictionary<string, string> { } headerTags)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_HeaderTags_Set);
                        var currentTags = tracerSettings.HeaderTagsInternal;
                        // This is a replacement, so make sure to clear
                        // Could also use a setter
                        currentTags.Clear();
                        foreach (var tag in headerTags)
                        {
                            currentTags[tag.Key] = tag.Value;
                        }
                    }

                    break;

                case TracerSettingKeyConstants.GlobalTagsKey:
                    if (setting.Value is IDictionary<string, string> { } tags)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GlobalTags_Set);
                        var globalTags = tracerSettings.GlobalTagsInternal;
                        // This is a replacement, so make sure to clear
                        // Could also use a setter
                        globalTags.Clear();
                        foreach (var tag in tags)
                        {
                            globalTags[tag.Key] = tag.Value;
                        }
                    }

                    break;

                case TracerSettingKeyConstants.HttpClientErrorCodesKey:
                    if (setting.Value is IEnumerable<int> clientErrorCodes)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpClientErrorStatusCodes);
                        tracerSettings.SetHttpClientErrorStatusCodesInternal(clientErrorCodes);
                    }

                    break;

                case TracerSettingKeyConstants.HttpServerErrorCodesKey:
                    if (setting.Value is IEnumerable<int> serverErrorCodes)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpServerErrorStatusCodes);
                        tracerSettings.SetHttpServerErrorStatusCodesInternal(serverErrorCodes);
                    }

                    break;

                case TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set);
                    tracerSettings.KafkaCreateConsumerScopeEnabledInternal = (bool)setting.Value!;
                    break;

                case TracerSettingKeyConstants.LogsInjectionEnabledKey:
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
                    tracerSettings.LogsInjectionEnabled = (bool)setting.Value!;
#pragma warning restore DD0002
                    break;

                case TracerSettingKeyConstants.ServiceNameKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_ServiceName_Set);
                    tracerSettings.ServiceNameInternal = setting.Value as string;
                    break;

                case TracerSettingKeyConstants.ServiceNameMappingsKey:
                    if (setting.Value is Dictionary<string, string> mappings)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetServiceNameMappings);
                        tracerSettings.SetServiceNameMappingsInternal(mappings);
                    }

                    break;

                case TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set);
                    tracerSettings.MaxTracesSubmittedPerSecondInternal = (int)setting.Value!;
                    break;

                case TracerSettingKeyConstants.ServiceVersionKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_ServiceVersion_Set);
                    tracerSettings.ServiceVersionInternal = setting.Value as string;
                    break;

                case TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set);
                    tracerSettings.StartupDiagnosticLogEnabledInternal = (bool)setting.Value!;
                    break;

                case TracerSettingKeyConstants.StatsComputationEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_StatsComputationEnabled_Set);
                    tracerSettings.StatsComputationEnabledInternal = (bool)setting.Value!;
                    break;

                case TracerSettingKeyConstants.TraceEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TraceEnabled_Set);
                    tracerSettings.TraceEnabledInternal = (bool)setting.Value!;
                    break;

                case TracerSettingKeyConstants.TracerMetricsEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set);
                    tracerSettings.TracerMetricsEnabledInternal = (bool)setting.Value!;
                    break;

                case TracerSettingKeyConstants.IntegrationSettingsKey:
                    UpdateIntegrations(tracerSettings, setting.Value as Dictionary<string, object?[]>);
                    break;

                default:
                    Log.Warning("Unknown manual instrumentation key '{Key}' provided. Ignoring value '{Value}'", setting.Key, setting.Value);
                    break;
            }
#pragma warning restore DD0002
        }

        static void UpdateIntegrations(TracerSettings settings, Dictionary<string, object?[]>? updated)
        {
            if (updated is null || updated.Count == 0)
            {
                return;
            }

            var integrations = settings.IntegrationsInternal.Settings;

            foreach (var pair in updated)
            {
                if (!IntegrationRegistry.TryGetIntegrationId(pair.Key, out var integrationId))
                {
                    Log.Warning("Error updating integration {IntegrationName} from manual instrumentation - unknown integration ID", pair.Key);
                    continue;
                }

                var setting = integrations[(int)integrationId];

                if (IntegrationSettingsSerializationHelper.TryDeserializeFromManual(
                       pair.Value,
                       out var enabledChanged,
                       out var enabled,
                       out var analyticsEnabledChanged,
                       out var analyticsEnabled,
                       out var analyticsSampleRateChanged,
                       out var analyticsSampleRate))
                {
                    if (enabledChanged)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_Enabled_Set);
                        setting.EnabledInternal = enabled;
                    }

                    if (analyticsEnabledChanged)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set);
                        setting.AnalyticsEnabledInternal = analyticsEnabled;
                    }

                    if (analyticsSampleRateChanged)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Set);
                        setting.AnalyticsSampleRateInternal = analyticsSampleRate;
                    }
                }
            }
        }
    }
}
