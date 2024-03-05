// <copyright file="TracerSettings.ITracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.TracerSettingKeyConstants;
using static Datadog.Trace.Configuration.TracerSettingSerializationHelper;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains Tracer settings that can be set in code.
/// </summary>
public partial class TracerSettings // : ITracerSettings
{
    // The methods in this file are used to implement the ITracerSettings interface
    // But we can't _explicitly implement_ the interface because duck typing won't find them
    internal bool TryGetObject(string key, out object? value)
        => key switch
        {
            ObjectKeys.AgentUriKey => IsChanged(in _agentUri, out value),
            ObjectKeys.CustomSamplingRules => IsChanged(in _customSamplingRules, out value),
            ObjectKeys.EnvironmentKey => IsChanged(in _environment, out value),
            ObjectKeys.ServiceNameKey => IsChanged(in _serviceName, out value),
            ObjectKeys.ServiceVersionKey => IsChanged(in _serviceVersion, out value),

            ObjectKeys.GlobalTagsKey => IsChanged(_globalTags, out value),
            ObjectKeys.GrpcTags => IsChanged(_grpcTags, out value),
            ObjectKeys.HeaderTags => IsChanged(_headerTags, out value),
            ObjectKeys.DisabledIntegrationNamesKey => IsChanged(_disabledIntegrationNames, out value),

            ObjectKeys.IntegrationSettingsKey => BuildIntegrationSettings(Integrations, out value),

            // These are write-only, so only send if non null
            ObjectKeys.ServiceNameMappingsKey => IfNotNull(_serviceNameMappings, out value),
            ObjectKeys.HttpClientErrorCodesKey => IfNotNull(_httpClientErrorCodes, out value),
            ObjectKeys.HttpServerErrorCodesKey => IfNotNull(_httpServerErrorCodes, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetBool(string key, out bool value)
    {
        return key switch
        {
            BoolKeys.AnalyticsEnabledKey => IsChanged(_analyticsEnabled, out value),
            BoolKeys.KafkaCreateConsumerScopeEnabledKey => IsChanged(_kafkaCreateConsumerScopeEnabled, out value),
            BoolKeys.LogsInjectionEnabledKey => IsChanged(_logsInjectionEnabled, out value),
            BoolKeys.StartupDiagnosticLogEnabledKey => IsChanged(_startupDiagnosticLogEnabled, out value),
            BoolKeys.StatsComputationEnabledKey => IsChanged(_statsComputationEnabled, out value),
            BoolKeys.TraceEnabledKey => IsChanged(_traceEnabled, out value),
            BoolKeys.TracerMetricsEnabledKey => IsChanged(_tracerMetricsEnabled, out value),

            // This one is always sent
            BoolKeys.IsFromDefaultSourcesKey => Found(_isFromDefaultSources, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

        static bool Found(bool toSet, out bool val)
        {
            val = toSet;
            return true;
        }
    }

    internal bool TryGetInt(string key, out int value)
        => key switch
        {
            IntKeys.MaxTracesSubmittedPerSecondKey => IsChanged(_maxTracesSubmittedPerSecond, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetNullableDouble(string key, out double? value)
        => key switch
        {
            NullableDoubleKeys.GlobalSamplingRateKey => IsChanged(_globalSamplingRate, out value),

            // Key we don't know about. Should only happen in version-conflict situations
            _ => NotFound(out value),
        };

    internal bool TryGetDouble(string key, out double value) => NotFound(out value);

    internal bool TryGetNullableInt(string key, out int? value) => NotFound(out value);

    internal bool TryGetNullableBool(string key, out bool? value) => NotFound(out value);
}
