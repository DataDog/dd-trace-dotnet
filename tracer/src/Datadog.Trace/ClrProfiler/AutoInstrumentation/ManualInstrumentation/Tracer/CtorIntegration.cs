// <copyright file="CtorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Void Datadog.Trace.Tracer::Configure(System.Collections.Generic.Dictionary`2[System.String,System.Object]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.Object, "System.Collections.Generic.Dictionary`2[System.String,System.Object]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = "3.6.*", // Removed in 3.7.0
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CtorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object? automaticTracer, Dictionary<string, object?> values)
    {
        // In earlier versions of the automatic tracer, the settings were passed in as a dictionary
        // but in 3.7.0 we don't need to instrument them
        if (automaticTracer is Datadog.Trace.Tracer tracer)
        {
            PopulateSettings(values, tracer.Settings);
        }

        return CallTargetState.GetDefault();
    }

    internal static void PopulateSettings(Dictionary<string, object?> values, TracerSettings settings)
    {
        // record all the settings in the dictionary
        // This key is used to detect if the settings have been populated _at all_, so should always be sent
        values[TracerSettingKeyConstants.AgentUriKey] = settings.Exporter.AgentUri;
#pragma warning disable CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.AnalyticsEnabledKey] = settings.MutableSettings.AnalyticsEnabled;
#pragma warning restore CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.CustomSamplingRules] = settings.MutableSettings.CustomSamplingRules;
        values[TracerSettingKeyConstants.DiagnosticSourceEnabledKey] = GlobalSettings.Instance.DiagnosticSourceEnabled;
        values[TracerSettingKeyConstants.EnvironmentKey] = settings.MutableSettings.Environment;
        values[TracerSettingKeyConstants.GlobalSamplingRateKey] = settings.MutableSettings.GlobalSamplingRate;
        values[TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey] = settings.MutableSettings.KafkaCreateConsumerScopeEnabled;
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
        values[TracerSettingKeyConstants.LogsInjectionEnabledKey] = settings.MutableSettings.LogsInjectionEnabled;
#pragma warning restore DD0002
        values[TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey] = settings.MutableSettings.MaxTracesSubmittedPerSecond;
        values[TracerSettingKeyConstants.ServiceNameKey] = settings.MutableSettings.ServiceName;
        values[TracerSettingKeyConstants.ServiceVersionKey] = settings.MutableSettings.ServiceVersion;
        values[TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey] = settings.MutableSettings.StartupDiagnosticLogEnabled;
        values[TracerSettingKeyConstants.StatsComputationEnabledKey] = settings.StatsComputationEnabled;
        values[TracerSettingKeyConstants.TraceEnabledKey] = settings.MutableSettings.TraceEnabled;
        values[TracerSettingKeyConstants.TracerMetricsEnabledKey] = settings.MutableSettings.TracerMetricsEnabled;

        // probably don't _have_ to copy these dictionaries, but playing it safe
        values[TracerSettingKeyConstants.GlobalTagsKey] = new ConcurrentDictionary<string, string>(settings.MutableSettings.GlobalTags);
        values[TracerSettingKeyConstants.GrpcTags] = new ConcurrentDictionary<string, string>(settings.MutableSettings.GrpcTags);
        values[TracerSettingKeyConstants.HeaderTags] = new ConcurrentDictionary<string, string>(settings.MutableSettings.HeaderTags);

        values[TracerSettingKeyConstants.IntegrationSettingsKey] = BuildIntegrationSettings(settings.MutableSettings.Integrations);
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
            results[setting.IntegrationName] = [setting.Enabled, setting.AnalyticsEnabled, setting.AnalyticsSampleRate];
        }

        return results;
    }
}
