// <copyright file="TraceContextTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tests.Util;

internal static class TraceContextTestHelpers
{
    public static TraceContext CreateTraceContextWithRootSpan(ulong traceIdLower)
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)traceIdLower, spanId: RandomIdGenerator.Shared.NextSpanId());
        var rootSpan = new Span(spanContext, DateTimeOffset.UtcNow);
        traceContext.AddSpan(rootSpan);
        return traceContext;
    }
}
