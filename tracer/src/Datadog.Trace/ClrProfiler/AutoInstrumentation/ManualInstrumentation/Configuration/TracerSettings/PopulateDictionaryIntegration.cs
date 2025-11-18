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

    [TestingAndPrivateOnly]
    internal static void PopulateSettings(Dictionary<string, object?> values, Trace.Configuration.TracerSettings settings)
    {
        // record all the settings in the dictionary
        values[TracerSettingKeyConstants.AgentUriKey] = settings.Exporter.AgentUri;
#pragma warning disable CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.AnalyticsEnabledKey] = settings.AnalyticsEnabled;
#pragma warning restore CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.CustomSamplingRules] = settings.CustomSamplingRules;
        values[TracerSettingKeyConstants.DiagnosticSourceEnabledKey] = GlobalSettings.Instance.DiagnosticSourceEnabled;
        values[TracerSettingKeyConstants.DisabledIntegrationNamesKey] = settings.DisabledIntegrationNames;
        values[TracerSettingKeyConstants.EnvironmentKey] = settings.Environment;
        values[TracerSettingKeyConstants.GlobalSamplingRateKey] = settings.GlobalSamplingRate;
        values[TracerSettingKeyConstants.GrpcTags] = settings.GrpcTags;
        values[TracerSettingKeyConstants.HeaderTags] = settings.HeaderTags;
        values[TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey] = settings.KafkaCreateConsumerScopeEnabled;
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
        values[TracerSettingKeyConstants.LogsInjectionEnabledKey] = settings.LogsInjectionEnabled;
#pragma warning restore DD0002
        values[TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey] = settings.MaxTracesSubmittedPerSecond;
        values[TracerSettingKeyConstants.ServiceNameKey] = settings.ServiceName;
        values[TracerSettingKeyConstants.ServiceVersionKey] = settings.ServiceVersion;
        values[TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey] = settings.StartupDiagnosticLogEnabled;
        values[TracerSettingKeyConstants.StatsComputationEnabledKey] = settings.StatsComputationEnabled;
        values[TracerSettingKeyConstants.TraceEnabledKey] = settings.TraceEnabled;
        values[TracerSettingKeyConstants.TracerMetricsEnabledKey] = settings.TracerMetricsEnabled;

        values[TracerSettingKeyConstants.GlobalTagsKey] = settings.GlobalTags;
        values[TracerSettingKeyConstants.IntegrationSettingsKey] = BuildIntegrationSettings(settings.Integrations);
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
