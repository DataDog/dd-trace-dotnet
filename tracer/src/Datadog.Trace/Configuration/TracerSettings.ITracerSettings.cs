// <copyright file="TracerSettings.ITracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.TracerSettingKeyConstants;
using static Datadog.Trace.Configuration.ManualInstrumentationSerializationHelper;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains Tracer settings.
/// </summary>
public partial class TracerSettings // : ITracerSettings
{
    // The methods in this file are used to implement the ITracerSettings interface
    // But we can't _explicitly implement_ the interface because duck typing won't find them
    internal bool TryGetObject(string key, out object? value)
        => key switch
        {
            ObjectKeys.AgentUriKey => Found(ExporterInternal.AgentUriInternal, out value),
            ObjectKeys.CustomSamplingRules => Found(CustomSamplingRulesInternal, out value),
            ObjectKeys.EnvironmentKey => Found(EnvironmentInternal, out value),
            ObjectKeys.ServiceNameKey => Found(ServiceNameInternal, out value),
            ObjectKeys.ServiceVersionKey => Found(ServiceVersionInternal, out value),

            ObjectKeys.GlobalTagsKey => Found(GlobalTagsInternal, out value),
            ObjectKeys.GrpcTags => Found(GrpcTagsInternal, out value),
            ObjectKeys.HeaderTags => Found(HeaderTagsInternal, out value),
            ObjectKeys.DisabledIntegrationNamesKey => Found(DisabledIntegrationNamesInternal, out value),

            ObjectKeys.IntegrationSettingsKey => BuildIntegrationSettings(IntegrationsInternal, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetBool(string key, out bool value)
        => key switch
        {
#pragma warning disable CS0618 // Type or member is obsolete
            BoolKeys.AnalyticsEnabledKey => Found(AnalyticsEnabledInternal, out value),
#pragma warning restore CS0618
            BoolKeys.DiagnosticSourceEnabledKey => Found(GlobalSettings.Instance.DiagnosticSourceEnabled, out value),
            BoolKeys.KafkaCreateConsumerScopeEnabledKey => Found(KafkaCreateConsumerScopeEnabledInternal, out value),
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally (there's no internal version currently)
            BoolKeys.LogsInjectionEnabledKey => Found(LogSubmissionSettings.LogsInjectionEnabled, out value),
#pragma warning restore DD0002
            BoolKeys.StartupDiagnosticLogEnabledKey => Found(StartupDiagnosticLogEnabledInternal, out value),
            BoolKeys.StatsComputationEnabledKey => Found(StatsComputationEnabledInternal, out value),
            BoolKeys.TraceEnabledKey => Found(TraceEnabledInternal, out value),
            BoolKeys.TracerMetricsEnabledKey => Found(TracerMetricsEnabledInternal, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetInt(string key, out int value)
        => key switch
        {
            IntKeys.MaxTracesSubmittedPerSecondKey => Found(MaxTracesSubmittedPerSecondInternal, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetNullableDouble(string key, out double? value)
        => key switch
        {
            NullableDoubleKeys.GlobalSamplingRateKey => Found(GlobalSamplingRateInternal, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetDouble(string key, out double value) => NotFound(out value);

    internal bool TryGetNullableInt(string key, out int? value) => NotFound(out value);

    internal bool TryGetNullableBool(string key, out bool? value) => NotFound(out value);
}
