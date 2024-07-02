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
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CtorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object? automaticTracer, Dictionary<string, object?> values)
    {
        if (automaticTracer is Datadog.Trace.Tracer tracer)
        {
            PopulateSettings(values, tracer.Settings);
        }

        return CallTargetState.GetDefault();
    }

    internal static void PopulateSettings(Dictionary<string, object?> values, ImmutableTracerSettings settings)
    {
        // record all the settings in the dictionary
        values[TracerSettingKeyConstants.AgentUriKey] = settings.ExporterInternal.AgentUriInternal;
#pragma warning disable CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.AnalyticsEnabledKey] = settings.AnalyticsEnabledInternal;
#pragma warning restore CS0618 // Type or member is obsolete
        values[TracerSettingKeyConstants.CustomSamplingRules] = settings.CustomSamplingRulesInternal;
        values[TracerSettingKeyConstants.DiagnosticSourceEnabledKey] = GlobalSettings.Instance.DiagnosticSourceEnabled;
        values[TracerSettingKeyConstants.EnvironmentKey] = settings.EnvironmentInternal;
        values[TracerSettingKeyConstants.GlobalSamplingRateKey] = settings.GlobalSamplingRateInternal;
        values[TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey] = settings.KafkaCreateConsumerScopeEnabledInternal;
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
        values[TracerSettingKeyConstants.LogsInjectionEnabledKey] = settings.LogsInjectionEnabledInternal;
#pragma warning restore DD0002
        values[TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey] = settings.MaxTracesSubmittedPerSecondInternal;
        values[TracerSettingKeyConstants.ServiceNameKey] = settings.ServiceNameInternal;
        values[TracerSettingKeyConstants.ServiceVersionKey] = settings.ServiceVersionInternal;
        values[TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey] = settings.StartupDiagnosticLogEnabledInternal;
        values[TracerSettingKeyConstants.StatsComputationEnabledKey] = settings.StatsComputationEnabledInternal;
        values[TracerSettingKeyConstants.TraceEnabledKey] = settings.TraceEnabledInternal;
        values[TracerSettingKeyConstants.TracerMetricsEnabledKey] = settings.TracerMetricsEnabledInternal;

        // probably don't _have_ to copy these dictionaries, but playing it safe
        values[TracerSettingKeyConstants.GlobalTagsKey] = new ConcurrentDictionary<string, string>(settings.GlobalTagsInternal);
        values[TracerSettingKeyConstants.GrpcTags] = new ConcurrentDictionary<string, string>(settings.GrpcTagsInternal);
        values[TracerSettingKeyConstants.HeaderTags] = new ConcurrentDictionary<string, string>(settings.HeaderTagsInternal);

        values[TracerSettingKeyConstants.IntegrationSettingsKey] = BuildIntegrationSettings(settings.IntegrationsInternal);
    }

    private static Dictionary<string, object?[]>? BuildIntegrationSettings(ImmutableIntegrationSettingsCollection settings)
    {
        if (settings.Settings.Length == 0)
        {
            return null;
        }

        var results = new Dictionary<string, object?[]>(settings.Settings.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings.Settings)
        {
            results[setting.IntegrationNameInternal] = [setting.EnabledInternal, setting.AnalyticsEnabledInternal, setting.AnalyticsSampleRateInternal];
        }

        return results;
    }
}
