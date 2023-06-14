// <copyright file="Count.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "It's easier to read")]
[TelemetryMetricType(TelemetryMetricType.Count)]
internal enum Count
{
#region General Namespace

    /// <summary>
    /// The number of logs created with a given log level. Useful for calculating impact for other features (automatic sending of logs)
    /// </summary>
    [TelemetryMetric<MetricTags.LogLevel>("log_created")] LogCreated,
#endregion
#region Tracers Namespace

    /// <summary>
    /// TODO: This is SUPPOSED to be tagged by IntegrationComponent too, but that's very onerous to implement right now so only adding 2 tags
    /// The number of errors/failures in the library integration, tagged by the integration Name (e.g. Kafka, RabbitMQ) and Errortype (e.g. Ducktype, Instrumentation)
    /// </summary>
    [TelemetryMetric<MetricTags.IntegrationName, MetricTags.InstrumentationError>("integrations_error")] IntegrationsError,

    /// <summary>
    /// The number of spans created by the tracer, tagged by integration name (or Manual) for custom spans
    /// </summary>
    [TelemetryMetric<MetricTags.IntegrationName>("span_created")] SpanCreated,

    /// <summary>
    /// The number of spans finished. When coupled with spans_created, can be used to identify leaks.
    /// </summary>
    [TelemetryMetric("span_finished")] SpanFinished,

    /// <summary>
    /// The number of spans sampled (kept) for serialization/flushing
    /// </summary>
    [TelemetryMetric("span_sampled")] SpanSampled,

    /// <summary>
    /// The number of spans dropped and the reason for being dropped (SamplingDecision, SingleSpanSampling, Failure)
    /// </summary>
    [TelemetryMetric<MetricTags.DropReason>("span_dropped")] SpanDropped,

    /// <summary>
    /// The number of traces created, tagged with new/continued depending on whether this is a new trace (no distributed context information) or continued (has distributed context).
    /// </summary>
    [TelemetryMetric<MetricTags.TraceContinuation>("trace_created")] TraceCreated,

    /// <summary>
    /// The number of traces enqueued. When coupled with trace_created, can be used to identify leaks
    /// </summary>
    [TelemetryMetric("trace_enqueued")] TraceEnqueued,

    /// <summary>
    /// The number of traces kept for serialization. Excludes single-span sampling spans.
    /// </summary>
    [TelemetryMetric("trace_sampled")] TraceSampled,

    /// <summary>
    /// The number of traces prior to sending, tagged by the reason it was dropped (OverfullBuffer, SerializationError, UnSampled)
    /// </summary>
    [TelemetryMetric<MetricTags.DropReason>("trace_dropped")] TraceDropped,

    /// <summary>
    /// The number of traces sent to the backend, regardless of response
    /// </summary>
    [TelemetryMetric("trace_sent")] TraceSent,

    /// <summary>
    /// The number of requests sent to the trace endpoint in the agent, regardless of success
    /// </summary>
    [TelemetryMetric("trace_api.requests")] TraceApiRequests,

    /// <summary>
    /// The number of responses received from the trace endpoint, tagged with status code.
    /// </summary>
    [TelemetryMetric<MetricTags.StatusCode>("trace_api.responses")] TraceApiResponses,

    /// <summary>
    /// The number of requests sent to the trace endpoint in the agent that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code)
    /// </summary>
    [TelemetryMetric<MetricTags.ApiError>("trace_api.errors")] TraceApiErrors,

    /// <summary>
    /// The number of times a partial flush (where a span is separately from its local root span) is triggered, tagged by the reason the flush was triggered (LargeTrace, SingleSpanIngestion)
    /// </summary>
    [TelemetryMetric<MetricTags.PartialFlushReason>("trace_partial_flush")] TracePartialFlush,

    /// <summary>
    /// The number of times distributed context is injected into an outgoing span, tagged by header style (tracecontext, Datadog, b3multi, b3 single header)
    /// </summary>
    [TelemetryMetric<MetricTags.ContextHeaderStyle>("context_header_style.injected")] ContextHeaderStyleInjected,

    /// <summary>
    /// The number of times distributed context is extracted from an incoming context, tagged by header style (tracecontext, Datadog, b3multi, b3 single header)
    /// </summary>
    [TelemetryMetric<MetricTags.ContextHeaderStyle>("context_header_style.extracted")] ContextHeaderStyleExtracted,

    /// <summary>
    /// The number of requests sent to the stats endpoint in the agent, regardless of success
    /// </summary>
    [TelemetryMetric("stats_api.requests")] StatsApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code.
    /// </summary>
    [TelemetryMetric<MetricTags.StatusCode>("stats_api.responses")] StatsApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint in the agent that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code)
    /// </summary>
    [TelemetryMetric<MetricTags.ApiError>("stats_api.errors")] StatsApiErrors,
#endregion
#region Telemetry Namespace

    /// <summary>
    /// The number of requests sent to a telemetry endopint, regardless of success, tagged by the endpoint (Agent, Agentless)
    /// </summary>
    [TelemetryMetric<MetricTags.TelemetryEndpoint>("telemetry_api.requests")] TelemetryApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code and endpoint (Agent, Agentless)
    /// </summary>
    [TelemetryMetric<MetricTags.TelemetryEndpoint, MetricTags.StatusCode>("telemetry_api.responses")] TelemetryApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint in the agent that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code) and endpoint (Agent, Agentless)
    /// </summary>
    [TelemetryMetric<MetricTags.TelemetryEndpoint, MetricTags.ApiError>("telemetry_api.errors")] TelemetryApiErrors,
#endregion
#region .NET Namespace

    /// <summary>
    /// The number of version-conflict tracers created
    /// </summary>
    [TelemetryMetric("version_conflict_tracer_created", isCommon: false)] VersionConflictTracerCreated,

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
    [TelemetryMetric<MetricTags.ApiError>("direct_log_api.errors.responses", isCommon: false)] DirectLogApiErrors,

#endregion
}
