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
using Datadog.Trace.Util;

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
                    tracerSettings.Exporter.AgentUriInternal = (setting.Value as Uri)!;
                    break;

                case TracerSettingKeyConstants.AnalyticsEnabledKey:
#pragma warning disable CS0618 // Type or member is obsolete
                    var boolValue = (bool)setting.Value!;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_AnalyticsEnabled_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.GlobalAnalyticsEnabled, boolValue, ConfigurationOrigins.Code);
                    tracerSettings.AnalyticsEnabled = boolValue;
#pragma warning restore CS0618 // Type or member is obsolete
                    break;

                case TracerSettingKeyConstants.CustomSamplingRules:
                    var rulesAsString = setting.Value as string;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_CustomSamplingRules_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.CustomSamplingRules, rulesAsString, recordValue: true, ConfigurationOrigins.Code);
                    tracerSettings.CustomSamplingRules = rulesAsString;
                    break;

                case TracerSettingKeyConstants.DiagnosticSourceEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set);
                    // there is no setter, it doesn't do anything
                    break;

                case TracerSettingKeyConstants.DisabledIntegrationNamesKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set);
                    var hashset = setting.Value as HashSet<string>;
                    var stringified = hashset is null ? null : string.Join(",", hashset);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.DisabledIntegrations, stringified, recordValue: true, ConfigurationOrigins.Code);
                    tracerSettings.DisabledIntegrationNames = hashset ?? [];
                    break;

                case TracerSettingKeyConstants.EnvironmentKey:
                    var envAsString = setting.Value as string;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Environment_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.Environment, envAsString, recordValue: true, ConfigurationOrigins.Code);
                    tracerSettings.Environment = envAsString;
                    break;

                case TracerSettingKeyConstants.GlobalSamplingRateKey:
                    var rateAsDouble = setting.Value as double?;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GlobalSamplingRate_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.GlobalSamplingRate, rateAsDouble, ConfigurationOrigins.Code);
                    tracerSettings.GlobalSamplingRate = rateAsDouble;
                    break;

                case TracerSettingKeyConstants.GrpcTags:
                    if (setting.Value is IDictionary<string, string> { } grpcTags)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GrpcTags_Set);
                        var currentTags = tracerSettings.GrpcTags;
                        // This is a replacement, so make sure to clear
                        // Could also use a setter
                        currentTags.Clear();
                        foreach (var tag in grpcTags)
                        {
                            currentTags[tag.Key] = tag.Value;
                        }

                        tracerSettings.Telemetry.Record(ConfigurationKeys.GrpcTags, Stringify(grpcTags), recordValue: true, ConfigurationOrigins.Code);
                    }

                    break;

                case TracerSettingKeyConstants.HeaderTags:
                    if (setting.Value is IDictionary<string, string> { } headerTags)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_HeaderTags_Set);
                        var currentTags = tracerSettings.HeaderTags;
                        // This is a replacement, so make sure to clear
                        // Could also use a setter
                        currentTags.Clear();
                        foreach (var tag in headerTags)
                        {
                            currentTags[tag.Key] = tag.Value;
                        }

                        tracerSettings.Telemetry.Record(ConfigurationKeys.HeaderTags, Stringify(headerTags), recordValue: true, ConfigurationOrigins.Code);
                    }

                    break;

                case TracerSettingKeyConstants.GlobalTagsKey:
                    if (setting.Value is IDictionary<string, string> { } tags)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GlobalTags_Set);
                        var globalTags = tracerSettings.GlobalTags;
                        // This is a replacement, so make sure to clear
                        // Could also use a setter
                        globalTags.Clear();
                        foreach (var tag in tags)
                        {
                            globalTags[tag.Key] = tag.Value;
                        }

                        tracerSettings.Telemetry.Record(ConfigurationKeys.GlobalTags, Stringify(tags), recordValue: true, ConfigurationOrigins.Code);
                    }

                    break;

                case TracerSettingKeyConstants.HttpClientErrorCodesKey:
                    if (setting.Value is IEnumerable<int> clientErrorCodes)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpClientErrorStatusCodes);
                        // this one currently records telemetry in the called method: we can tidy that up later
                        tracerSettings.SetHttpClientErrorStatusCodesInternal(clientErrorCodes);
                    }

                    break;

                case TracerSettingKeyConstants.HttpServerErrorCodesKey:
                    if (setting.Value is IEnumerable<int> serverErrorCodes)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpServerErrorStatusCodes);
                        // this one currently records telemetry in the called method: we can tidy that up later
                        tracerSettings.SetHttpServerErrorStatusCodesInternal(serverErrorCodes);
                    }

                    break;

                case TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey:
                    var kafkaScopeEnabled = (bool)setting.Value!;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, kafkaScopeEnabled, ConfigurationOrigins.Code);
                    tracerSettings.KafkaCreateConsumerScopeEnabled = kafkaScopeEnabled;
                    break;

                case TracerSettingKeyConstants.LogsInjectionEnabledKey:
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
                    // this one currently records telemetry in the called method: we can tidy that up later
                    tracerSettings.LogsInjectionEnabled = (bool)setting.Value!;
#pragma warning restore DD0002
                    break;

                case TracerSettingKeyConstants.ServiceNameKey:
                    var serviceName = setting.Value as string;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_ServiceName_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.ServiceName, serviceName, recordValue: true, ConfigurationOrigins.Code);
                    tracerSettings.ServiceName = serviceName;
                    break;

                case TracerSettingKeyConstants.ServiceNameMappingsKey:
                    if (setting.Value is Dictionary<string, string> mappings)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetServiceNameMappings);
                        // this one currently records telemetry in the called method: we can tidy that up later
                        tracerSettings.SetServiceNameMappingsInternal(mappings);
                    }

                    break;

                case TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set);
                    var tracesPerSecond = (int)setting.Value!;
                    tracerSettings.Telemetry.Record(ConfigurationKeys.TraceRateLimit, tracesPerSecond, ConfigurationOrigins.Code);
                    tracerSettings.MaxTracesSubmittedPerSecond = tracesPerSecond;
                    break;

                case TracerSettingKeyConstants.ServiceVersionKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_ServiceVersion_Set);
                    var serviceVersion = setting.Value as string;
                    tracerSettings.Telemetry.Record(ConfigurationKeys.ServiceVersion, serviceVersion, recordValue: true, ConfigurationOrigins.Code);
                    tracerSettings.ServiceVersion = serviceVersion;
                    break;

                case TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey:
                    var logEnabled = (bool)setting.Value!;
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.StartupDiagnosticLogEnabled, logEnabled, ConfigurationOrigins.Code);
                    tracerSettings.StartupDiagnosticLogEnabled = logEnabled;
                    break;

                case TracerSettingKeyConstants.StatsComputationEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_StatsComputationEnabled_Set);
                    var statsComputation = (bool)setting.Value!;
                    tracerSettings.Telemetry.Record(ConfigurationKeys.StatsComputationEnabled, statsComputation, ConfigurationOrigins.Code);
                    tracerSettings.StatsComputationEnabled = statsComputation;
                    break;

                case TracerSettingKeyConstants.TraceEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TraceEnabled_Set);
                    var traceEnabled = (bool)setting.Value!;
                    tracerSettings.Telemetry.Record(ConfigurationKeys.TraceEnabled, traceEnabled, ConfigurationOrigins.Code);
                    tracerSettings.TraceEnabled = traceEnabled;
                    break;

                case TracerSettingKeyConstants.TracerMetricsEnabledKey:
                    TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set);
                    var metricsEnabled = (bool)setting.Value!;
                    tracerSettings.Telemetry.Record(ConfigurationKeys.TracerMetricsEnabled, metricsEnabled, ConfigurationOrigins.Code);
                    tracerSettings.TracerMetricsEnabled = metricsEnabled;
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

        static string Stringify(IDictionary<string, string> from)
        {
            var sb = StringBuilderCache.Acquire();
            foreach (var tag in from)
            {
                sb.Append(tag.Key ?? string.Empty)
                  .Append(':')
                  .Append(tag.Value ?? string.Empty)
                  .Append(',');
            }

            if (sb.Length > 0)
            {
                sb.Length--;
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        static void UpdateIntegrations(TracerSettings settings, Dictionary<string, object?[]>? updated)
        {
            if (updated is null || updated.Count == 0)
            {
                return;
            }

            var integrations = settings.Integrations.Settings;

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
                        setting.Enabled = enabled;
                    }

#pragma warning disable 618 // App analytics is deprecated, but still used
                    if (analyticsEnabledChanged)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set);
                        setting.AnalyticsEnabled = analyticsEnabled;
                    }

                    if (analyticsSampleRateChanged)
                    {
                        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Set);
                        setting.AnalyticsSampleRate = analyticsSampleRate;
                    }
#pragma warning restore 618
                }
            }
        }
    }
}
