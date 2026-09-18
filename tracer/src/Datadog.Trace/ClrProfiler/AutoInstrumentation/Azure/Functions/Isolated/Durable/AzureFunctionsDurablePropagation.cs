// <copyright file="AzureFunctionsDurablePropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

internal static class AzureFunctionsDurablePropagation
{
    private const byte RecordedFlag = 1;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionsDurablePropagation));

    internal static PropagationContext ExtractPropagatedContext<TFunctionContext>(TFunctionContext functionContext)
        where TFunctionContext : IDurableFunctionContext
    {
        try
        {
            if (functionContext.TraceContext is not { } rawTraceContext
             || !rawTraceContext.TryDuckCast<IWorkerTraceContext>(out var traceContext))
            {
                return default;
            }

            var traceParent = traceContext.TraceParent;
            if (StringUtil.IsNullOrEmpty(traceParent))
            {
                return default;
            }

            var traceState = traceContext.TraceState;
            var datadogSamplingPriority = W3CTraceContextPropagator.ParseTraceState(traceState).SamplingPriority;

            // Durable's AllData listener can clear the W3C recorded flag while retaining Datadog's
            // positive sampling decision. Restore the flag before extraction can override that decision.
            if (datadogSamplingPriority is > 0)
            {
                traceParent = RestoreRecordedFlag(traceParent);
            }

            var context = ExtractHeaders(traceParent, traceState);

            // Without a Datadog decision, let the local sampler decide instead of inheriting
            // Azure's implicit rejection. W3C headers cannot represent an undecided sampling priority.
            if (datadogSamplingPriority is null)
            {
                context = ClearImplicitSamplingRejection(context);
            }

            return context;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting Azure Durable Functions trace context");
            return default;
        }
    }

    [TestingAndPrivateOnly]
    internal static string RestoreRecordedFlag(string traceParent)
    {
        var flagsStart = traceParent.LastIndexOf('-') + 1;
        if (flagsStart <= 0
         || traceParent.Length - flagsStart != 2
         || !HexString.TryParseByte(traceParent.AsSpan(flagsStart, 2), out var flags)
         || (flags & RecordedFlag) != 0)
        {
            return traceParent;
        }

        var reconciledTraceParent = traceParent.ToCharArray();
        var reconciledFlags = (flags | RecordedFlag).ToString("x2", CultureInfo.InvariantCulture);
        reconciledTraceParent[flagsStart] = reconciledFlags[0];
        reconciledTraceParent[flagsStart + 1] = reconciledFlags[1];
        return new string(reconciledTraceParent);
    }

    private static PropagationContext ExtractHeaders(string traceParent, string? traceState)
        => Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
            (TraceParent: traceParent, TraceState: traceState),
            GetHeaderValues);

    private static IEnumerable<string?> GetHeaderValues((string TraceParent, string? TraceState) context, string name)
    {
        if (name.Equals("traceparent", StringComparison.OrdinalIgnoreCase))
        {
            return [context.TraceParent];
        }

        if (name.Equals("tracestate", StringComparison.OrdinalIgnoreCase))
        {
            return [context.TraceState];
        }

        return [];
    }

    private static PropagationContext ClearImplicitSamplingRejection(PropagationContext context)
    {
        if (context.SpanContext is not { SamplingPriority: SamplingPriorityValues.AutoReject } spanContext)
        {
            return context;
        }

        // SamplingPriority is read-only. Copy the context to clear it while preserving all propagated metadata.
        var contextWithoutSamplingDecision = new SpanContext(
            traceId: spanContext.TraceId128,
            spanId: spanContext.SpanId,
            samplingPriority: null,
            serviceName: spanContext.ServiceName,
            origin: spanContext.Origin,
            rawTraceId: spanContext.RawTraceId,
            rawSpanId: spanContext.RawSpanId,
            isRemote: spanContext.IsRemote)
        {
            PropagatedTags = spanContext.PropagatedTags,
            AdditionalW3CTraceState = spanContext.AdditionalW3CTraceState,
            LastParentId = spanContext.LastParentId,
            ServiceNameSource = spanContext.ServiceNameSource,
        };

        return new PropagationContext(contextWithoutSamplingDecision, context.Baggage, context.Links);
    }
}

#endif
