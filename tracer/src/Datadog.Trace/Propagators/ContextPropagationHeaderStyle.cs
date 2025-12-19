// <copyright file="ContextPropagationHeaderStyle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Propagators;

/// <summary>
/// Values used to indicate propagators (aka header styles) in user settings.
/// </summary>
internal static class ContextPropagationHeaderStyle
{
    /// <summary>
    /// The W3C tracecontext propagator
    /// Uses headers: traceparent, tracestate.
    /// </summary>
    /// <seealso cref="W3CTraceContextPropagator"/>
    public const string W3CTraceContext = "tracecontext";

    /// <summary>
    /// The W3C baggage propagator.
    /// Uses headers: baggage.
    /// </summary>
    /// <seealso cref="W3CBaggagePropagator"/>
    public const string W3CBaggage = "baggage";

    /// <summary>
    /// The original Datadog propagator.
    /// Uses headers: x-datadog-trace-id, x-datadog-parent-id, x-datadog-sampling-priority,
    /// x-datadog-origin, x-datadog-tags.
    /// </summary>
    /// <seealso cref="DatadogContextPropagator"/>
    public const string Datadog = "Datadog";

    /// <summary>
    /// The B3 propagator using multiple headers.
    /// Uses headers: X-B3-TraceId, X-B3-SpanId, X-B3-Sampled.
    /// </summary>
    /// <seealso cref="B3MultipleHeaderContextPropagator"/>
    public const string B3MultipleHeaders = "b3multi";

    /// <summary>
    /// The B3 propagator using a single header.
    /// Uses headers: b3.
    /// </summary>
    /// <seealso cref="B3SingleHeaderContextPropagator"/>
    public const string B3SingleHeader = "b3 single header";

    /// <summary>
    /// Deprecated values used to indicate propagation header styles.
    /// </summary>
    internal static class Deprecated
    {
        /// <summary>
        /// Deprecated value for the W3C tracecontext propagation header style.
        /// Uses headers: traceparent, tracestate.
        /// Use <see cref="ContextPropagationHeaderStyle.W3CTraceContext"/> instead.
        /// </summary>
        public const string W3CTraceContext = "W3C";

        /// <summary>
        /// The deprecated value for the B3 propagation header style using multiple headers.
        /// Uses headers: X-B3-TraceId, X-B3-SpanId, X-B3-Sampled.
        /// Use <see cref="ContextPropagationHeaderStyle.B3MultipleHeaders"/> instead.
        /// </summary>
        public const string B3MultipleHeaders = "B3";

        /// <summary>
        /// The deprecated value for the B3 propagation header style using a single header.
        /// Uses headers: b3.
        /// Use <see cref="ContextPropagationHeaderStyle.B3SingleHeader"/> instead.
        /// </summary>
        public const string B3SingleHeader = "B3SingleHeader";
    }
}
