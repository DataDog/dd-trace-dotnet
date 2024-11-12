// <copyright file="PopulateDictionaryIntegration.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.TracerSettings;

/// <summary>
/// System.Boolean Datadog.Trace.Configuration.TracerSettings::PopulateDictionary() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.TracerSettings",
    MethodName = "PopulateDictionary",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.Dictionary`2[System.String,System.Object]", ClrNames.Bool],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PopulateDictionaryIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(Dictionary<string, object?> values, bool useDefaultSources)
    {
        var settings = useDefaultSources
                           ? Trace.Configuration.TracerSettings.FromDefaultSourcesInternal()
                           : new Trace.Configuration.TracerSettings(null, new ConfigurationTelemetry(), new OverrideErrorLog());

        PopulateSettings(values, settings);

        return CallTargetState.GetDefault();
    }

    // Internal for testing
    internal static void PopulateSettings(Dictionary<string, object?> values, Trace.Configuration.TracerSettings settings)
    {
        // record all the settings in the dictionary
        values[TracerSettingKeyConstants.AgentUriKey] = settings.ExporterInternal.AgentUriInternal;
#pragma warning disable CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.AnalyticsEnabledKey] = settings.AnalyticsEnabledInternal;
#pragma warning restore CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.CustomSamplingRules] = settings.CustomSamplingRulesInternal;
        values[TracerSettingKeyConstants.DiagnosticSourceEnabledKey] = GlobalSettings.Instance.DiagnosticSourceEnabled;
        values[TracerSettingKeyConstants.DisabledIntegrationNamesKey] = settings.DisabledIntegrationNamesInternal;
        values[TracerSettingKeyConstants.EnvironmentKey] = settings.EnvironmentInternal;
        values[TracerSettingKeyConstants.GlobalSamplingRateKey] = settings.GlobalSamplingRateInternal;
        values[TracerSettingKeyConstants.GrpcTags] = settings.GrpcTagsInternal;
        values[TracerSettingKeyConstants.HeaderTags] = settings.HeaderTagsInternal;
        values[TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey] = settings.KafkaCreateConsumerScopeEnabledInternal;
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
        values[TracerSettingKeyConstants.LogsInjectionEnabledKey] = settings.LogSubmissionSettings.LogsInjectionEnabled;
#pragma warning restore DD0002
        values[TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey] = settings.MaxTracesSubmittedPerSecondInternal;
        values[TracerSettingKeyConstants.ServiceNameKey] = settings.ServiceNameInternal;
        values[TracerSettingKeyConstants.ServiceVersionKey] = settings.ServiceVersionInternal;
        values[TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey] = settings.StartupDiagnosticLogEnabledInternal;
        values[TracerSettingKeyConstants.StatsComputationEnabledKey] = settings.StatsComputationEnabledInternal;
        values[TracerSettingKeyConstants.TraceEnabledKey] = settings.TraceEnabledInternal;
        values[TracerSettingKeyConstants.TracerMetricsEnabledKey] = settings.TracerMetricsEnabledInternal;

        values[TracerSettingKeyConstants.GlobalTagsKey] = settings.GlobalTagsInternal;
        values[TracerSettingKeyConstants.IntegrationSettingsKey] = BuildIntegrationSettings(settings.IntegrationsInternal);
    }

    private static Dictionary<string, object?[]>? BuildIntegrationSettings(IntegrationSettingsCollection settings)
    {
        if (settings.Settings.Length == 0)
        {
            return null;
        }

        var results = new Dictionary<string, object?[]>(settings.Settings.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings.Settings)
        {
            results[setting.IntegrationNameInternal] = IntegrationSettingsSerializationHelper.SerializeFromAutomatic(setting.EnabledInternal, setting.AnalyticsEnabledInternal, setting.AnalyticsSampleRateInternal);
        }

        return results;
    }
}
