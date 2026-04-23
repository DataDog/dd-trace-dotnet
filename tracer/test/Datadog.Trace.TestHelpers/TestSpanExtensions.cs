// <copyright file="TestSpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;

namespace Datadog.Trace;

/// <summary>
/// Compatibility shim for test code written against the pre-"non-recording-spans" Span/Tracer API.
///
/// This branch removed these instance APIs from Datadog.Trace:
/// - Span(SpanContext, ...) constructor (only RecordedSpanContext overload remains)
/// - Tracer.StartSpan(string operationName, ...) overloads
/// - Tracer.StartActiveInternal(string operationName, ...) overloads
/// - Mutable Span.ResourceName / Span.OperationName (root-level Tags/SetService still exist on Span)
///
/// Tests have not been rewritten to use the new two-step pipeline (CreateSpanContext → StartSpan(RecordedSpanContext)).
/// These extension + factory methods let old-style test code compile against the new API. Tests aren't
/// expected to pass — the goal is only that the test projects build.
/// </summary>
internal static class TestSpanExtensions
{
    // ----- Factory helpers -----

    /// <summary>
    /// Test-compat factory: creates a recorded <see cref="Span"/> from a plain <see cref="SpanContext"/>.
    /// Old code used <c>new Span(spanContext, startTime)</c>; the new API requires a <see cref="RecordedSpanContext"/>.
    /// </summary>
    public static Span CreateSpan(
        SpanContext context,
        DateTimeOffset? start = null,
        ITags? tags = null,
        string? operationName = null,
        string? resourceName = null,
        IEnumerable<SpanLink>? links = null)
        => new Span(new RecordedSpanContext(context, operationName, resourceName), start, tags, links);

    /// <summary>
    /// Test-compat factory: creates a recorded <see cref="Span"/> from any <see cref="MaybeRecordedSpanContext"/>.
    /// </summary>
    public static Span CreateSpan(
        MaybeRecordedSpanContext context,
        DateTimeOffset? start = null,
        ITags? tags = null)
        => new Span(context as RecordedSpanContext ?? new RecordedSpanContext(context.Context, context.OperationName, context.ResourceName), start, tags);

    // ----- Tracer extension overloads restoring old string-based API -----

    /// <summary>
    /// Test-compat: start a span from a string operation name.
    /// Always returns a recorded Span (drops the span and returns <c>null</c> on sampling reject).
    /// </summary>
    public static Span? StartSpan(
        this Tracer tracer,
        string operationName,
        ITags? tags = null,
        ISpanContext? parent = null,
        string? serviceName = null,
        DateTimeOffset? startTime = null,
        bool ignoreActiveScope = false,
        TraceId traceId = default,
        ulong spanId = 0,
        string? resourceName = null,
        bool addToTraceContext = true)
    {
        var ctx = tracer.CreateSpanContext(
            operationName,
            resourceName ?? operationName,
            (ignoreActiveScope && parent is null) ? SpanContext.None : parent,
            serviceName,
            serviceNameSource: serviceName is not null ? ServiceNameMetadata.Manual : null,
            traceId: traceId,
            spanId: spanId);

        return ctx is RecordedSpanContext recorded
            ? tracer.StartSpan(recorded, tags, startTime, addToTraceContext)
            : null;
    }

    /// <summary>
    /// Test-compat: start an active scope from a string operation name.
    /// </summary>
    public static Scope? StartActiveInternal(
        this Tracer tracer,
        string operationName,
        ISpanContext? parent = null,
        string? serviceName = null,
        DateTimeOffset? startTime = null,
        bool ignoreActiveScope = false,
        bool finishOnClose = true,
        ulong spanId = 0,
        string? resourceName = null,
        ITags? tags = null)
    {
        var ctx = tracer.CreateSpanContext(
            operationName,
            resourceName ?? operationName,
            (ignoreActiveScope && parent is null) ? SpanContext.None : parent,
            serviceName,
            serviceNameSource: serviceName is not null ? ServiceNameMetadata.Manual : null,
            spanId: spanId);

        return ctx switch
        {
            RecordedSpanContext recorded => tracer.StartActiveInternal(recorded, startTime, finishOnClose, tags),
            UnrecordedSpanContext unrecorded => tracer.StartActiveInternal(unrecorded, finishOnClose),
            _ => null,
        };
    }

    /// <summary>
    /// Test-compat: create a span context without supplying an operation name (now required on the instance API).
    /// </summary>
    public static MaybeRecordedSpanContext CreateSpanContext(
        this Tracer tracer,
        ISpanContext? parent,
        string? serviceName,
        TraceId traceId,
        ulong spanId)
        => tracer.CreateSpanContext(
            operationName: "operation",
            resourceName: null,
            parent: parent,
            serviceName: serviceName,
            traceId: traceId,
            spanId: spanId);

    // ----- SpanBase extensions (tests often receive Scope.Span as SpanBase) -----

    /// <summary>Safe cast helper.</summary>
    public static Span AsSpan(this SpanBase span) => (Span)span;

    public static ISpan SetTag(this SpanBase span, string key, string? value) => ((Span)span).SetTag(key, value);

    public static string? GetTag(this SpanBase span, string key) => ((Span)span).GetTag(key);

    public static double? GetMetric(this SpanBase span, string key) => ((Span)span).GetMetric(key);

    public static Span SetMetric(this SpanBase span, string key, double? value) => ((Span)span).SetMetric(key, value);

    public static void SetService(this SpanBase span, string serviceName, string? source) => ((Span)span).SetService(serviceName, source);

    public static void SetException(this SpanBase span, Exception exception) => ((Span)span).SetException(exception);

    public static ITags GetTags(this SpanBase span) => ((Span)span).Tags;

    public static DateTimeOffset GetStartTime(this SpanBase span) => ((Span)span).StartTime;

    public static TimeSpan GetDuration(this SpanBase span) => ((Span)span).Duration;

    public static bool GetIsFinished(this SpanBase span) => ((Span)span).IsFinished;

    public static bool GetError(this SpanBase span) => ((Span)span).Error;

    public static void SetError(this SpanBase span, bool value) => ((Span)span).Error = value;

    public static List<SpanLink>? GetSpanLinks(this SpanBase span) => ((Span)span).SpanLinks;
}
