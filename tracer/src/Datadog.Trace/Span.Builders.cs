// <copyright file="Span.Builders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal partial class Span
{
    private void SetSpanContextValues(ISpanContextInternal spanContext)
    {
        if (spanContext is Span span)
        {
            _parent = span._parent;
            _traceId128 = span._traceId128;
            _spanId = span._spanId;
            _serviceName = span._serviceName;
            _traceContext = span._traceContext;
            _propagatedTags = span._propagatedTags;
            _samplingPriority = span._samplingPriority;
            _rawTraceId = span._rawTraceId;
            _rawSpanId = span._rawSpanId;
            _origin = span._origin;
            _additionalW3CTraceState = span._additionalW3CTraceState;
            _pathwayContext = span._pathwayContext;
        }
        else if (spanContext is not null)
        {
            _parent = spanContext.Parent;
            _traceId128 = spanContext.TraceId128;
            _spanId = spanContext.SpanId;
            _serviceName = spanContext.ServiceName;
            _traceContext = spanContext.TraceContext;
            _propagatedTags = spanContext.PropagatedTags;
            _samplingPriority = spanContext.SamplingPriority;
            _rawTraceId = spanContext.RawTraceId;
            _rawSpanId = spanContext.RawSpanId;
            _origin = spanContext.Origin;
            _additionalW3CTraceState = spanContext.AdditionalW3CTraceState;
            _pathwayContext = spanContext.PathwayContext;
        }
    }

    internal static ISpanContextInternal CreateSpanContext(ulong? traceId, ulong spanId, SamplingPriority? samplingPriority = null, string serviceName = null)
    {
        var context = (Span)CreateSpanContext((TraceId)(traceId ?? 0), serviceName);
        // public ctor must keep accepting legacy types:
        // - traceId: ulong? => TraceId
        // - samplingPriority: SamplingPriority? => int?
        context._spanId = spanId;
        context._samplingPriority = (int?)samplingPriority;
        return context;
    }

    internal static ISpanContextInternal CreateSpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin)
    {
        var context = (Span)CreateSpanContext(traceId, serviceName);
        context._spanId = spanId;
        context._samplingPriority = samplingPriority;
        context._origin = origin;
        return context;
    }

    internal static ISpanContextInternal CreateSpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin, string rawTraceId, string rawSpanId)
    {
        var context = (Span)CreateSpanContext(traceId, serviceName);
        context._spanId = spanId;
        context._samplingPriority = samplingPriority;
        context._origin = origin;
        context._rawTraceId = rawTraceId;
        context._rawSpanId = rawSpanId;
        return context;
    }

    internal static ISpanContextInternal CreateSpanContext(ISpanContext parent, TraceContext traceContext, string serviceName, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null)
    {
        var context = (Span)CreateSpanContext(GetTraceId(parent, traceId), serviceName);

        // if 128-bit trace ids are enabled, also use full uint64 for span id,
        // otherwise keep using the legacy so-called uint63s.
        var useAllBits = traceContext?.Tracer?.Settings?.TraceId128BitGenerationEnabled ?? false;

        context._spanId = spanId > 0 ? spanId : RandomIdGenerator.Shared.NextSpanId(useAllBits);
        context._parent = parent;
        context._traceContext = traceContext;

        if (parent is Span parentSpan)
        {
            context._rawTraceId = parentSpan._rawTraceId ?? rawTraceId;
            context._pathwayContext = parentSpan._pathwayContext;
        }
        else if (parent is ISpanContextInternal spanContextInternal)
        {
            context._rawTraceId = spanContextInternal.RawTraceId ?? rawTraceId;
            context._pathwayContext = spanContextInternal.PathwayContext;
        }
        else
        {
            context._rawTraceId = rawTraceId;
        }

        context._rawSpanId = rawSpanId;
        return context;
    }

    internal static ISpanContextInternal CreateSpanContext(TraceId traceId, string serviceName)
    {
        var context = new Span();
        context._traceId128 = traceId == Trace.TraceId.Zero
                                 ? RandomIdGenerator.Shared.NextTraceId(useAllBits: false)
                                 : traceId;

        context._serviceName = serviceName;

        // Because we have a ctor as part of the public api without accepting the origin tag,
        // we need to ensure new SpanContext created by this .ctor has the CI Visibility origin
        // tag if the CI Visibility mode is running to ensure the correct propagation
        // to children spans and distributed trace.
        if (CIVisibility.IsRunning)
        {
            ((ISpanContextInternal)context).Origin = Ci.Tags.TestTags.CIAppTestOriginName;
        }

        return context;
    }

    internal static Span CreateSpan(ISpanContextInternal context, DateTimeOffset? start)
    {
        return CreateSpan(context, start, null);
    }

    internal static Span CreateSpan(ISpanContextInternal context, DateTimeOffset? start, ITags tags)
    {
        if (context is Span span)
        {
            span.Tags = tags ?? new CommonTags();
            span.StartTime = start ?? context.TraceContext.UtcNow;
        }
        else
        {
            span = new Span();
            span.Tags = tags ?? new CommonTags();
            span.SetSpanContextValues(context);
            span.StartTime = start ?? context.TraceContext.UtcNow;
        }

        if (IsLogLevelDebugEnabled)
        {
            var tagsType = span.Tags.GetType();
            Log.Debug(
                "Span started: [s_id: {SpanId}, p_id: {ParentId}, t_id: {TraceId}] with Tags: [{Tags}], Tags Type: [{TagsType}])",
                new object[] { span.Context.RawSpanId, span.Context.ParentId, span.Context.RawTraceId, span.Tags, tagsType });
        }

        return span;
    }

    private static TraceId GetTraceId(ISpanContext context, TraceId fallback)
    {
        return context switch
        {
            // if there is no context or it has a zero trace id,
            // use the specified fallback value
            null or { TraceId: 0 } => fallback,

            // use the 128-bit trace id from SpanContext if possible
            ISpanContextInternal sc => sc.TraceId128,

            // otherwise use the 64-bit trace id from ISpanContext
            _ => (TraceId)context.TraceId
        };
    }
}
