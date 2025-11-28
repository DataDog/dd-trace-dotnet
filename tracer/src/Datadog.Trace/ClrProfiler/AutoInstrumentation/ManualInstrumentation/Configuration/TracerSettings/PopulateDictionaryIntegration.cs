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
using Datadog.Trace.SourceGenerators;

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
public static class PopulateDictionaryIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(Dictionary<string, object?> values, bool useDefaultSources)
    {
        // This creates a "one off", "throw away" instance of the tracer settings, which ignores previous configuration etc
        // However, we _can_ reuse the existing "global" tracer settings if they use default sources, and just use the "initial"
        // settings for the "mutable" settings
        var settings = useDefaultSources
                           ? Datadog.Trace.Tracer.Instance.Settings
                           : new Trace.Configuration.TracerSettings(null, new ConfigurationTelemetry(), new OverrideErrorLog());

        PopulateSettings(values, settings);

        return CallTargetState.GetDefault();
    }

    [TestingAndPrivateOnly]
    internal static void PopulateSettings(Dictionary<string, object?> values, Trace.Configuration.TracerSettings settings)
    {
        // record all the settings in the dictionary
        var mutableSettings = settings.Manager.InitialMutableSettings;
        var exporterSettings = settings.Manager.InitialExporterSettings;
        values[TracerSettingKeyConstants.AgentUriKey] = exporterSettings.AgentUri;
#pragma warning disable CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.AnalyticsEnabledKey] = mutableSettings.AnalyticsEnabled;
#pragma warning restore CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.CustomSamplingRules] = mutableSettings.CustomSamplingRules;
        values[TracerSettingKeyConstants.DiagnosticSourceEnabledKey] = GlobalSettings.Instance.DiagnosticSourceEnabled;
        values[TracerSettingKeyConstants.DisabledIntegrationNamesKey] = mutableSettings.DisabledIntegrationNames;
        values[TracerSettingKeyConstants.EnvironmentKey] = mutableSettings.Environment;
        values[TracerSettingKeyConstants.GlobalSamplingRateKey] = mutableSettings.GlobalSamplingRate;
        values[TracerSettingKeyConstants.GrpcTags] = mutableSettings.GrpcTags;
        values[TracerSettingKeyConstants.HeaderTags] = mutableSettings.HeaderTags;
        values[TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey] = mutableSettings.KafkaCreateConsumerScopeEnabled;
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
        values[TracerSettingKeyConstants.LogsInjectionEnabledKey] = mutableSettings.LogsInjectionEnabled;
#pragma warning restore DD0002
        values[TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey] = mutableSettings.MaxTracesSubmittedPerSecond;
        values[TracerSettingKeyConstants.ServiceNameKey] = mutableSettings.ServiceName;
        values[TracerSettingKeyConstants.ServiceVersionKey] = mutableSettings.ServiceVersion;
        values[TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey] = mutableSettings.StartupDiagnosticLogEnabled;
        values[TracerSettingKeyConstants.StatsComputationEnabledKey] = settings.StatsComputationEnabled;
        values[TracerSettingKeyConstants.TraceEnabledKey] = mutableSettings.TraceEnabled;
        values[TracerSettingKeyConstants.TracerMetricsEnabledKey] = mutableSettings.TracerMetricsEnabled;

        values[TracerSettingKeyConstants.GlobalTagsKey] = mutableSettings.GlobalTags;
        values[TracerSettingKeyConstants.IntegrationSettingsKey] = BuildIntegrationSettings(mutableSettings.Integrations);
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
            results[setting.IntegrationName] = IntegrationSettingsSerializationHelper.SerializeFromAutomatic(setting.Enabled, setting.AnalyticsEnabled, setting.AnalyticsSampleRate);
        }

        return results;
    }
}
