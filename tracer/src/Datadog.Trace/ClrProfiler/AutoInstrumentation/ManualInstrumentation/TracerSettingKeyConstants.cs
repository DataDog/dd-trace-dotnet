// <copyright file="TracerSettingKeyConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

internal class TracerSettingKeyConstants
{
    // These keys are used when sending from manual to automatic, but not in reverse
    public const string IsFromDefaultSourcesKey = "IsFromDefaultSources";
    public const string HttpClientErrorCodesKey = "DD_HTTP_CLIENT_ERROR_STATUSES";
    public const string HttpServerErrorCodesKey = "DD_HTTP_SERVER_ERROR_STATUSES";
    public const string ServiceNameMappingsKey = "DD_TRACE_SERVICE_MAPPING";

    // These keys are used when sending from automatic to manual, but not in reverse
    public const string DiagnosticSourceEnabledKey = "DD_DIAGNOSTIC_SOURCE_ENABLED";

    // These are used in both directions
    public const string IntegrationSettingsKey = "IntegrationSettings";
    public const string AgentUriKey = "DD_TRACE_AGENT_URL";
    public const string AnalyticsEnabledKey = "DD_TRACE_ANALYTICS_ENABLED";
    public const string CustomSamplingRules = "DD_TRACE_SAMPLING_RULES";
    public const string DisabledIntegrationNamesKey = "DD_DISABLED_INTEGRATIONS";
    public const string EnvironmentKey = "DD_ENV";
    public const string GlobalSamplingRateKey = "DD_TRACE_SAMPLE_RATE";
    public const string GlobalTagsKey = "DD_TAGS";
    public const string GrpcTags = "DD_TRACE_GRPC_TAGS";
    public const string HeaderTags = "DD_TRACE_HEADER_TAGS";
    public const string KafkaCreateConsumerScopeEnabledKey = "DD_TRACE_KAFKA_CREATE_CONSUMER_SCOPE_ENABLED";
    public const string LogsInjectionEnabledKey = "DD_LOGS_INJECTION";
    public const string MaxTracesSubmittedPerSecondKey = "DD_TRACE_RATE_LIMIT";
    public const string ServiceNameKey = "DD_SERVICE";
    public const string ServiceVersionKey = "DD_VERSION";
    public const string StartupDiagnosticLogEnabledKey = "DD_TRACE_STARTUP_LOGS";
    public const string StatsComputationEnabledKey = "DD_TRACE_STATS_COMPUTATION_ENABLED";
    public const string TraceEnabledKey = "DD_TRACE_ENABLED";
    public const string TracerMetricsEnabledKey = "DD_TRACE_METRICS_ENABLED";
}
