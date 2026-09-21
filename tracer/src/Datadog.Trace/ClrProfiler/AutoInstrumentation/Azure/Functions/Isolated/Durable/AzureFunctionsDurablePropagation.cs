// <copyright file="AzureFunctionsDurablePropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (functionContext.TraceContext is not { } traceContext)
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

            // W3C recorded flag always reaches us as 0, use the datadog decision when positive.
            if (datadogSamplingPriority is > 0)
            {
                traceParent = EnsureTraceParentSampledFlag(traceParent);
            }

            var context = ExtractHeaders(traceParent, traceState);

            // Without a Datadog decision, let the local sampler decide instead of inheriting
            // Azure's implicit rejection.
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

    // Sets the W3C recorded (sampled) bit when the trace-flags field is valid, preserving the rest of traceparent.
    [TestingAndPrivateOnly]
    internal static string EnsureTraceParentSampledFlag(string traceParent)
    {
        // W3C trace-flags occupy positions 53-54
        // https://github.com/DataDog/dd-trace-dotnet/blob/v3.53.0/tracer/src/Datadog.Trace/Propagators/W3CTraceContextPropagator.cs#L289
        const int flagsStart = 53;
        const int flagsEnd = flagsStart + 2;
        var header = traceParent.Trim();

        if (header.Length < flagsEnd
         || header[flagsStart - 1] != '-'
         || (header.Length > flagsEnd && header[flagsEnd] != '-')
         || !HexString.TryParseByte(header.AsSpan(flagsStart, 2), out var flags)
         || (flags & RecordedFlag) != 0)
        {
            return traceParent;
        }

        var sampledFlags = (flags | RecordedFlag).ToString("x2", CultureInfo.InvariantCulture);

        // The span-based overload is unavailable on some target frameworks. Follow the existing pattern:
        // https://github.com/DataDog/dd-trace-dotnet/blob/v3.53.0/tracer/src/Datadog.Trace/Debugger/Symbols/SymbolsUploader.cs#L443-L447
#if NETCOREAPP
        return string.Concat(header.AsSpan(0, flagsStart), sampledFlags, header.AsSpan(flagsEnd));
#else
        return header.Substring(0, flagsStart) + sampledFlags + header.Substring(flagsEnd);
#endif
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

    // Clears Azure Durable's implicit sampling rejection so the local sampler can decide.
    private static PropagationContext ClearImplicitSamplingRejection(PropagationContext context)
    {
        if (context.SpanContext is not { SamplingPriority: SamplingPriorityValues.AutoReject } spanContext)
        {
            return context;
        }

        // SamplingPriority is read-only
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
            OtelTraceState = spanContext.OtelTraceState,
            LastParentId = spanContext.LastParentId,
            ServiceNameSource = spanContext.ServiceNameSource,
        };

        return new PropagationContext(contextWithoutSamplingDecision, context.Baggage, context.Links);
    }
}

#endif
