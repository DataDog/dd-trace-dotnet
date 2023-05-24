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
#region Tracers Namespace

    /// <summary>
    /// The number of errors/failures in the library integration, tagged by the integration Name (e.g. Kafka, RabbitMQ), integration component (e.g. KafkaConsumerConsumeIntegration), and Errortype (e.g. Ducktype, Instrumentation)
    /// </summary>
    [TelemetryMetric("integrations_error", 3)] IntegrationsError,

    /// <summary>
    /// The number of spans created by the tracer, tagged by integration name and component (or Manual) for custom spans
    /// </summary>
    [TelemetryMetric("span_created", 2)] SpanCreated,

    /// <summary>
    /// The number of spans finished, tagged by integration name and component (or Manual) for custom spans. When coupled with spans_created, can be used to identify leaks.
    /// </summary>
    [TelemetryMetric("span_finished", 0)] SpanFinished,

    /// <summary>
    /// The number of spans sampled (kept) for serialization/flushing
    /// </summary>
    [TelemetryMetric("span_sampled", 0)] SpanSampled,

    /// <summary>
    /// The number of spans dropped and the reason for being dropped (SamplingDecision, SingleSpanSampling, Failure)
    /// </summary>
    [TelemetryMetric("span_dropped", 1)] SpanDropped,

    /// <summary>
    /// The number of traces created, tagged with new/continued depending on whether this is a new trace (no distributed context information) or continued (has distributed context).
    /// </summary>
    [TelemetryMetric("trace_created", 1)] TraceCreated,

    /// <summary>
    /// The number of traces enqueued. When coupled with trace_created, can be used to identify leaks
    /// </summary>
    [TelemetryMetric("trace_enqueued", 0)] TraceEnqueued,

    /// <summary>
    /// The number of traces kept for serialization. Excludes single-span sampling spans.
    /// </summary>
    [TelemetryMetric("trace_sampled", 0)] TraceSampled,

    /// <summary>
    /// The number of traces prior to sending, tagged by the reason it was dropped (OverfullBuffer, SerializationError, UnSampled)
    /// </summary>
    [TelemetryMetric("trace_dropped", 1)] TraceDropped,

    /// <summary>
    /// The number of traces sent to the backend, regardless of response
    /// </summary>
    [TelemetryMetric("trace_sent", 0)] TraceSent,

    /// <summary>
    /// The number of requests sent to the trace endpoint in the agent, regardless of success
    /// </summary>
    [TelemetryMetric("trace_api.requests", 0)] TraceApiRequests,

    /// <summary>
    /// The number of responses received from the trace endpoint, tagged with status code.
    /// </summary>
    [TelemetryMetric("trace_api.responses", 1)] TraceApiResponses,

    /// <summary>
    /// The number of requests sent to the trace endpoint in the agent that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code)
    /// </summary>
    [TelemetryMetric("trace_api.errors", 1)] TraceApiErrors,

    /// <summary>
    /// The number of times a partial flush (where a span is separately from its local root span) is triggered, tagged by the reason the flush was triggered (LargeTrace, SingleSpanIngestion)
    /// </summary>
    [TelemetryMetric("trace_partial_flush", 1)] TracePartialFlush,

    /// <summary>
    /// The number of spans included when partial flush is triggered
    /// </summary>
    [TelemetryMetric("trace_partial_flush.spans_closed", 1)] TracePartialFlushSpansClosed,

    /// <summary>
    /// The number of open spans remaining in the trace when partial flush is triggered
    /// </summary>
    [TelemetryMetric("trace_partial_flush.spans_remaining", 1)] TracePartialFlushSpansRemaining,

    /// <summary>
    /// The number of times distributed context is injected into an outgoing span, tagged by header style (tracecontext, Datadog, b3multi, b3 single header)
    /// </summary>
    [TelemetryMetric("context_header_style.injected", 1)] ContextHeaderStyleInjected,

    /// <summary>
    /// The number of times distributed context is extracted from an incoming context, tagged by header style (tracecontext, Datadog, b3multi, b3 single header)
    /// </summary>
    [TelemetryMetric("context_header_style.extracted", 1)] ContextHeaderStyleExtracted,

    /// <summary>
    /// The number of requests sent to the stats endpoint in the agent, regardless of success
    /// </summary>
    [TelemetryMetric("stats_api.requests", 1)] StatsApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code.
    /// </summary>
    [TelemetryMetric("stats_api.responses", 1)] StatsApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint in the agent that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code)
    /// </summary>
    [TelemetryMetric("stats_api.errors", 1)] StatsApiErrors,
#endregion
#region Telemetry Namespace

    /// <summary>
    /// The number of requests sent to a telemetry endopint, regardless of success, tagged by the endpoint (Agent, Agentless)
    /// </summary>
    [TelemetryMetric("telemetry_api.requests", 1)] TelemetryApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code and endpoint (Agent, Agentless)
    /// </summary>
    [TelemetryMetric("telemetry_api.responses", 2)] TelemetryApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint in the agent that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code) and endpoint (Agent, Agentless)
    /// </summary>
    [TelemetryMetric("telemetry_api.errors", 1)] TelemetryApiErrors,
#endregion
#region .NET Namespace

    /// <summary>
    /// The number of version-conflict traces created
    /// </summary>
    [TelemetryMetric("version_conflict_traces_created", 1, isCommon: false)] VersionConflictTracesCreated,

    /// <summary>
    /// The number of logs sent to the direct log submission sink, tagged by IntegrationName. Includes only logs that were sent, not filtered logs
    /// </summary>
    [TelemetryMetric("direct_log_logs", 1, isCommon: false)] DirectLogLogs,

    /// <summary>
    /// The number of requests sent to the direct log submission endpoint, regardless of success
    /// </summary>
    [TelemetryMetric("direct_log_api.requests", 0, isCommon: false)] DirectLogApiRequests,

    /// <summary>
    /// The number of responses received from the endpoint, tagged with status code
    /// </summary>
    [TelemetryMetric("direct_log_api.responses", 1, isCommon: false)] DirectLogApiResponses,

    /// <summary>
    /// The number of requests sent to the api endpoint that errored, tagged by the error type (e.g. Timeout, NetworkError, status_code)
    /// </summary>
    [TelemetryMetric("direct_log_api.errors.responses", 1, isCommon: false)] DirectLogApiErrors,

#endregion
}
