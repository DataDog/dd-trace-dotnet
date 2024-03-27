// <copyright file="LogContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Globalization;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging;

internal static class LogContext
{
    private static string GetTraceId(SpanContext context, bool use128Bits) =>
        use128Bits ?
            context.RawTraceId :
            context.TraceId128.Lower.ToString(CultureInfo.InvariantCulture);

    private static string GetSpanId(SpanContext context) =>
        context.SpanId.ToString(CultureInfo.InvariantCulture);

    public static bool TryGetValues(
        IReadOnlyDictionary<string, string> context,
        out string traceId,
        out string spanId,
        bool use128Bits)
    {
        if (context is SpanContext spanContext)
        {
            traceId = GetTraceId(spanContext, use128Bits);
            spanId = GetSpanId(spanContext);
            return true;
        }

        if (TryGetTraceId(context, use128Bits, out traceId) &&
            TryGetSpanId(context, out spanId))
        {
            return true;
        }

        traceId = string.Empty;
        spanId = string.Empty;
        return false;
    }

    public static bool TryGetTraceId(IReadOnlyDictionary<string, string> context, bool use128Bits, out string traceId)
    {
        if (context is SpanContext spanContext)
        {
            traceId = GetTraceId(spanContext, use128Bits);
            return true;
        }

        var traceIdKey = use128Bits ? SpanContext.Keys.RawTraceId : SpanContext.Keys.TraceId;

        // For mismatch version support we need to keep requesting old HttpHeaderNames keys.
        if (context.TryGetValue(traceIdKey, out var value) ||
            context.TryGetValue(HttpHeaderNames.TraceId, out value))
        {
            traceId = value;
            return true;
        }

        traceId = string.Empty;
        return false;
    }

    public static bool TryGetSpanId(IReadOnlyDictionary<string, string> context, out string spanId)
    {
        if (context is SpanContext spanContext)
        {
            spanId = GetSpanId(spanContext);
            return true;
        }

        // For mismatch version support we need to keep requesting old HttpHeaderNames keys.
        if (context.TryGetValue(SpanContext.Keys.ParentId, out var value) ||
            context.TryGetValue(HttpHeaderNames.ParentId, out value))
        {
            spanId = value;
            return true;
        }

        spanId = string.Empty;
        return false;
    }
}
