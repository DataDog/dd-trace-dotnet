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
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.TracerSettingKeyConstants;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Void Datadog.Trace.Tracer::Configure(System.Collections.Generic.Dictionary`2[System.String,System.Object]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "Configure",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Datadog.Trace.Configuration.TracerSettings"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ConfigureIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigureIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget, TSettings>(TSettings settings)
        where TSettings : ITracerSettings, IDuckType
    {
        // public method, so customer could pass in null technically
        if (settings.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        // Is this from calling new TracerSettings() or TracerSettings.Global?
        var isFromDefaults = settings.TryGetBool(BoolKeys.IsFromDefaultSourcesKey, out var value) && value is true;

        // Get the starting point
        var automatic = isFromDefaults
                           ? TracerSettings.FromDefaultSourcesInternal()
                           : new TracerSettings(null, new ConfigurationTelemetry());

        // Update the settings based on the values they have changed
        UpdateSettings(in settings, automatic);

        // Update the global instance
        Trace.Tracer.ConfigureInternal(new ImmutableTracerSettings(automatic, true));

        return CallTargetState.GetDefault();
    }

    // Internal for testing
    internal static void UpdateSettings<T>(in T manualSettings, TracerSettings tracerSettings)
        where T : ITracerSettings
    {
        // Bool keys
        bool extracted;
        if (manualSettings.TryGetBool(BoolKeys.AnalyticsEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set);
#pragma warning disable CS0618 // Type or member is obsolete
            tracerSettings.AnalyticsEnabledInternal = extracted;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        if (manualSettings.TryGetBool(BoolKeys.DiagnosticSourceEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set);
            // there is no setter, it doesn't do anything
        }

        if (manualSettings.TryGetBool(BoolKeys.KafkaCreateConsumerScopeEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set);
            tracerSettings.KafkaCreateConsumerScopeEnabledInternal = extracted;
        }

        if (manualSettings.TryGetBool(BoolKeys.LogsInjectionEnabledKey, out extracted))
        {
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
            tracerSettings.LogsInjectionEnabled = extracted;
#pragma warning restore DD0002
        }

        if (manualSettings.TryGetBool(BoolKeys.KafkaCreateConsumerScopeEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set);
            tracerSettings.KafkaCreateConsumerScopeEnabledInternal = extracted;
        }

        if (manualSettings.TryGetBool(BoolKeys.StartupDiagnosticLogEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set);
            tracerSettings.StartupDiagnosticLogEnabledInternal = extracted;
        }

        if (manualSettings.TryGetBool(BoolKeys.StatsComputationEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_StatsComputationEnabled_Set);
            tracerSettings.StatsComputationEnabledInternal = extracted;
        }

        if (manualSettings.TryGetBool(BoolKeys.TraceEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TraceEnabled_Set);
            tracerSettings.TraceEnabledInternal = extracted;
        }

        if (manualSettings.TryGetBool(BoolKeys.TracerMetricsEnabledKey, out extracted))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set);
            tracerSettings.TracerMetricsEnabledInternal = extracted;
        }

        // Int keys
        if (manualSettings.TryGetInt(IntKeys.MaxTracesSubmittedPerSecondKey, out var count))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set);
            tracerSettings.MaxTracesSubmittedPerSecondInternal = count;
        }

        // Double keys
        if (manualSettings.TryGetNullableDouble(NullableDoubleKeys.GlobalSamplingRateKey, out var rate))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_GlobalSamplingRate_Set);
            tracerSettings.GlobalSamplingRateInternal = rate;
        }

        // Object keys
        object? obj;
        if (manualSettings.TryGetObject(ObjectKeys.AgentUriKey, out obj))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_AgentUri_Set);
            tracerSettings.ExporterInternal.AgentUriInternal = (obj as Uri)!;
        }

        if (manualSettings.TryGetObject(ObjectKeys.CustomSamplingRules, out obj))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_CustomSamplingRules_Get);
            tracerSettings.CustomSamplingRulesInternal = obj as string;
        }

        if (manualSettings.TryGetObject(ObjectKeys.DisabledIntegrationNamesKey, out obj))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set);
            tracerSettings.DisabledIntegrationNamesInternal = obj as HashSet<string> ?? [];
        }

        if (manualSettings.TryGetObject(ObjectKeys.EnvironmentKey, out obj))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Environment_Set);
            tracerSettings.EnvironmentInternal = obj as string;
        }

        if (manualSettings.TryGetObject(ObjectKeys.ServiceNameKey, out obj))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_ServiceName_Set);
            tracerSettings.ServiceNameInternal = obj as string;
        }

        if (manualSettings.TryGetObject(ObjectKeys.ServiceVersionKey, out obj))
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_ServiceVersion_Set);
            tracerSettings.ServiceVersionInternal = obj as string;
        }

        if (manualSettings.TryGetObject(ObjectKeys.GrpcTags, out obj)
         && obj is IDictionary<string, string> grpcTags)
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

        if (manualSettings.TryGetObject(ObjectKeys.HeaderTags, out obj)
         && obj is IDictionary<string, string> headerTags)
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

        if (manualSettings.TryGetObject(ObjectKeys.GlobalTagsKey, out obj)
         && obj is IDictionary<string, string> tags)
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

        if (manualSettings.TryGetObject(ObjectKeys.HttpClientErrorCodesKey, out obj)
         && obj is IEnumerable<int> clientErrorCodes)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpClientErrorStatusCodes);
            tracerSettings.SetHttpClientErrorStatusCodesInternal(clientErrorCodes);
        }

        if (manualSettings.TryGetObject(ObjectKeys.HttpServerErrorCodesKey, out obj)
         && obj is IEnumerable<int> serverErrorCodes)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpServerErrorStatusCodes);
            tracerSettings.SetHttpServerErrorStatusCodesInternal(serverErrorCodes);
        }

        if (manualSettings.TryGetObject(ObjectKeys.ServiceNameMappingsKey, out obj)
         && obj is Dictionary<string, string> mappings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetServiceNameMappings);
            tracerSettings.SetServiceNameMappingsInternal(mappings);
        }

        if (manualSettings.TryGetObject(ObjectKeys.IntegrationSettingsKey, out obj)
         && obj is Dictionary<string, object?[]> integrations
            && integrations.Count != 0)
        {
            var integrations1 = tracerSettings.IntegrationsInternal.Settings;

            foreach (var pair in integrations)
            {
                if (!IntegrationRegistry.TryGetIntegrationId(pair.Key, out var integrationId))
                {
                    Log.Warning("Error updating integration {IntegrationName} from manual instrumentation - unknown integration ID", pair.Key);
                    continue;
                }

                var setting = integrations1[(int)integrationId];

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
