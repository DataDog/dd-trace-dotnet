// <copyright file="Count.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;
using NS = Datadog.Trace.Telemetry.MetricNamespaceConstants;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "It's easier to read")]
[TelemetryMetricType(TelemetryMetricType.Count)]
internal enum Count
{
#region General Namespace

    /// <summary>
    /// The number of logs created with a given log level. Useful for calculating impact for other features (automatic sending of logs). Levels should be one of `level:debug`, `level:info`, `level:warn`, `level:error`, `level:critical`
    /// </summary>
    [TelemetryMetric<MetricTags.LogLevel>("logs_created", isCommon: true, NS.General)] LogCreated,
#endregion
#region Tracers Namespace

    /// <summary>
    /// The number of spans created by the tracer, tagged by automatic integration name (e.g. `integration_name:kafka`, `integration_name:rabbitmq`) or manual API (`integration_name:datadog`, `integration_name:otel` or `integration_name:opentracing`)
    /// </summary>
    [TelemetryMetric<MetricTags.IntegrationName>("spans_created")] SpanCreated,

    /// <summary>
    /// The number of spans finished, optionally (if implementation allows) tagged by automatic integration name (e.g. `integration_name:kafka`, `integration_name:rabbitmq`) or manual API (`integration_name:datadog`, `integration_name:otel` or `integration_name:opentracing`)
    /// </summary>
    [TelemetryMetric("spans_finished")] SpanFinished,

    /// <summary>
    /// The number of spans enqueued for serialization/flushing. Tagged by one of `reason:p0_keep` (the span was part of a p0 trace that was kept for sending to the agent), `reason:single_span_sampling` (the span was selected via single_span_sampling, and otherwise would have been dropped as a p0 span), or `reason:default` - The tracer is not dropping p0 spans, so the span was enqueued 'by default' for sending to the trace-agent)
    /// </summary>
    [TelemetryMetric<MetricTags.SpanEnqueueReason>("spans_enqueued_for_serialization")] SpanEnqueuedForSerialization,

    /// <summary>
    /// The number of spans dropped and the reason for being dropped, for example `reason:p0_drop` (the span was part of a p0 trace that was dropped by the tracer), `reason:overfull_buffer` (the local buffer was full, and the span had to be dropped), `reason:serialization_error` (there was an error serializing the span and it had to be dropped)
    /// </summary>
    [TelemetryMetric<MetricTags.DropReason>("spans_dropped")] SpanDropped,

    /// <summary>
    /// The number of trace segments (local traces) created, tagged with new/continued depending on whether this is a new trace (no distributed context information) or continued (has distributed context).
    /// </summary>
    [TelemetryMetric<MetricTags.TraceContinuation>("trace_segments_created")] TraceSegmentCreated,

    /// <summary>
    /// "The number of trace chunks kept for serialization. Excludes single-span sampling spans. Tagged by one of `reason:p0_keep` (the trace was a p0 trace that was kept for sending to the agent) or `reason:default` - The tracer is not dropping p0 spans, so the span was enqueued 'by default' for sending to the trace-agent)
    /// </summary>
    [TelemetryMetric<MetricTags.TraceChunkEnqueueReason>("trace_chunks_enqueued_for_serialization")] TraceChunkEnqueued,

    /// <summary>
    /// The number of trace chunks dropped prior to serialization, tagged by reason. Includes traces which are dropped due to errors, overfull buffers, as well as due to sampling decision. For example `reason:p0_drop` (the span a p0 trace that was droped by the tracer), `reason:overfull_buffer` (the local buffer was full, and the trace chunk had to be dropped), `reason:serialization_error` (there was an error serializing the trace and it had to be dropped
    /// </summary>
    [TelemetryMetric<MetricTags.DropReason>("trace_chunks_dropped")] TraceChunkDropped,

    /// <summary>
    /// The number of trace chunks attempted to be sent to the backend, regardless of response",
    /// </summary>
    [TelemetryMetric("trace_chunks_sent")] TraceChunkSent,

    /// <summary>
    /// The number of trace segments (local traces) closed. In non partial flush scenarios, trace_segments_closed == trace_chunks_enqueued",
    /// </summary>
    [TelemetryMetric("trace_segments_closed")] TraceSegmentsClosed,

    /// <summary>
    /// The number of requests sent to the trace endpoint in the agent, regardless of success
    /// </summary>
    [TelemetryMetric("trace_api.requests")] TraceApiRequests,

    /// <summary>
    /// The number of responses received from the trace endpoint, tagged with status code.
    /// </summary>
    [TelemetryMetric<MetricTags.StatusCode>("trace_api.responses")] TraceApiResponses,

    /// <summary>
    /// The number of requests sent to the trace endpoint in the agent that errored, tagged by the error type (e.g. `type:timeout`, `type:network`, `type:status_code`)
    /// </summary>
    [TelemetryMetric<MetricTags.ApiError>("trace_api.errors")] TraceApiErrors,

    /// <summary>
    /// The number of times a partial flush (where a span is flushed separately from its local root span) is triggered, tagged by the reason the flush was triggered (`reason:large_trace`, `reason:single_span_ingestion`)
    /// </summary>
    [TelemetryMetric<MetricTags.PartialFlushReason>("trace_partial_flush.count")] TracePartialFlush,

    /// <summary>
    /// The number of times distributed context is injected into an outgoing span, tagged by header style (`header_style:tracecontext`, `header_style:datadog`, `header_style:b3multi`, `header_style:b3single`)
    /// </summary>
    [TelemetryMetric<MetricTags.ContextHeaderStyle>("context_header_style.injected")] ContextHeaderStyleInjected,

    /// <summary>
    /// The number of times distributed context is successfully extracted from an outgoing span, tagged by header style (`header_style:tracecontext`, `header_style:datadog`, `header_style:b3multi`, `header_style:b3single`)
    /// </summary>
    [TelemetryMetric<MetricTags.ContextHeaderStyle>("context_header_style.extracted")] ContextHeaderStyleExtracted,

    /// <summary>
    /// The number of requests sent to the stats endopint in the agent, regardless of success
    /// </summary>
    [TelemetryMetric("stats_api.requests")] StatsApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code, e.g. `status_code:200`, `status_code:404`. May also use `status_code:5xx` for example as a catch-all for 2xx, 3xx, 4xx, 5xx responses
    /// </summary>
    [TelemetryMetric<MetricTags.StatusCode>("stats_api.responses")] StatsApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint in the agent that errored, tagged by the error type (e.g. `type:timeout`, `type:network`, `type:status_code`)
    /// </summary>
    [TelemetryMetric<MetricTags.ApiError>("stats_api.errors")] StatsApiErrors,

    /// <summary>
    /// The number of times a Datadog configuration is set while a corresponding OpenTelemetry configuration is set.
    /// </summary>
    [TelemetryMetric<MetricTags.DatadogConfiguration, MetricTags.OpenTelemetryConfiguration>("otel.env.hiding", isCommon: true, NS.Tracer)] OpenTelemetryConfigHiddenByDatadogConfig,

    /// <summary>
    /// The number of times an OpenTelemetry configuration has a mapping to a Datadog configuration but it cannot be mapped correctly.
    /// </summary>
    [TelemetryMetric<MetricTags.DatadogConfiguration, MetricTags.OpenTelemetryConfiguration>("otel.env.invalid", isCommon: true, NS.Tracer)] OpenTelemetryConfigInvalid,
#endregion
#region Telemetry Namespace

    /// <summary>
    /// "The number of requests sent to a telemetry endopint, regardless of success, tagged by the endpoint (`endpoint:agent`, `endpoint:agentless`)
    /// </summary>
    [TelemetryMetric<MetricTags.TelemetryEndpoint>("telemetry_api.requests", isCommon: true, NS.Telemetry)] TelemetryApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged endpoint (`endpoint:agent`, `endpoint:agentless`) and status code e.g. `status_code:200`, `status_code:404`. May also use `status_code:5xx` for example as a catch-all for 2xx, 3xx, 4xx, 5xx responses
    /// </summary>
    [TelemetryMetric<MetricTags.TelemetryEndpoint, MetricTags.StatusCode>("telemetry_api.responses", isCommon: true, NS.Telemetry)] TelemetryApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint in the agent that errored, tagged by the error type (e.g. `type:timeout`, `type:network`, `type:status_code`) and Endpoint (`endpoint:agent`, `endpoint:agentless`)
    /// </summary>
    [TelemetryMetric<MetricTags.TelemetryEndpoint, MetricTags.ApiError>("telemetry_api.errors", isCommon: true, NS.Telemetry)] TelemetryApiErrors,
#endregion
#region .NET Namespace

    /// <summary>
    /// The number of version-conflict tracers created
    /// </summary>
    [TelemetryMetric("version_conflict_tracers_created", isCommon: false)] VersionConflictTracerCreated,

    /// <summary>
    /// The number of services in which the customer has installed manual instrumentation that is greater than the automatic instrumentation (and so won't work)
    /// </summary>
    [TelemetryMetric("unsupported_custom_instrumentation_services", isCommon: false)] UnsupportedCustomInstrumentationServices,

    /// <summary>
    /// The number of logs sent to the direct log submission sink, tagged by IntegrationName. Includes only logs that were sent, not filtered logs
    /// </summary>
    [TelemetryMetric<MetricTags.IntegrationName>("direct_log_logs", isCommon: false)] DirectLogLogs,

    /// <summary>
    /// The number of requests sent to the direct log submission endpoint, regardless of success
    /// </summary>
    [TelemetryMetric("direct_log_api.requests", isCommon: false)] DirectLogApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code
    /// </summary>
    [TelemetryMetric<MetricTags.StatusCode>("direct_log_api.responses", isCommon: false)] DirectLogApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code)
    /// </summary>
    [TelemetryMetric<MetricTags.ApiError>("direct_log_api.errors", isCommon: false)] DirectLogApiErrors,

#endregion
#region AppSec Namespace

    /// <summary>
    /// The number of times the waf is initialized
    /// </summary>
    [TelemetryMetric("waf.init", isCommon: true, NS.ASM)] WafInit,

    /// <summary>
    /// The number of times we reload the rules (startup + Remote Configuration)
    /// </summary>
    [TelemetryMetric("waf.updates", isCommon: true, NS.ASM)] WafUpdates,

    /// <summary>
    /// Requests analyzed by ddwaf
    /// </summary>
    [TelemetryMetric<MetricTags.WafAnalysis>("waf.requests", isCommon: true, NS.ASM)] WafRequests,

    /// <summary>
    /// Waf inputs that have been truncated
    /// </summary>
    [TelemetryMetric<MetricTags.TruncationReason>("waf.input_truncated", isCommon: true, NS.ASM)] InputTruncated,

    /// <summary>
    /// Counts the number of times a rule type is evaluated.
    /// </summary>
    [TelemetryMetric<RaspRuleType>("rasp.rule.eval", isCommon: true, NS.ASM)] RaspRuleEval,

    /// <summary>
    /// Counts the number of times a rule type has a match. Note that this can be inferred through the events sent to the backend.
    /// </summary>
    [TelemetryMetric<RaspRuleType>("rasp.rule.match", isCommon: true, NS.ASM)] RaspRuleMatch,

    /// <summary>
    /// Counts the number of times a timeout was hit when evaluating a specific rule type.
    /// </summary>
    [TelemetryMetric<RaspRuleType>("rasp.timeout", isCommon: true, NS.ASM)] RaspTimeout,

    /// <summary>
    /// Counts the number of times a timeout was hit when evaluating a specific rule type.
    /// </summary>
    [TelemetryMetric<AuthenticationFramework>("instrum.user_auth.missing_user_id", isCommon: true, NS.ASM)] MissingUserId,

#endregion
#region Iast Namespace

    /// <summary>
    /// Counts the number of source methods that have been called
    /// </summary>
    [TelemetryMetric<MetricTags.IastInstrumentedSources>("executed.source", isCommon: true, NS.Iast)] IastExecutedSources,

    /// <summary>
    /// Counts the number of proopagation methods that have been called
    /// </summary>
    [TelemetryMetric("executed.propagation", isCommon: true, NS.Iast)] IastExecutedPropagations,

    /// <summary>
    /// Counts the number of sinks that have been called
    /// </summary>
    [TelemetryMetric<MetricTags.IastInstrumentedSinks>("executed.sink", isCommon: true, NS.Iast)] IastExecutedSinks,

    /// <summary>
    /// Counts the number of tainted objects after a request
    /// </summary>
    [TelemetryMetric("request.tainted", isCommon: true, NS.Iast)] IastRequestTainted,
#endregion
}
