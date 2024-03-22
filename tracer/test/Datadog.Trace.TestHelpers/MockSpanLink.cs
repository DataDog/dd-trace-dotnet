// <copyright file="MockSpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers;

public class MockSpanLink
{
    internal MockSpanLink(TraceId traceId, ulong spanId)
    {
        SpanId = spanId;
        TraceId = traceId;
    }

    internal TraceId TraceId { get; }

    internal ulong SpanId { get;  }
}
