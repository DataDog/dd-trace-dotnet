// <copyright file="MockSpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using MessagePack;

namespace Datadog.Trace.TestHelpers
{
    [MessagePackObject]
    [DebuggerDisplay("{ToString(),nq}")]
    internal class MockSpanLink
    {
        public MockSpanLink(TraceId traceId, ulong spanId)
        {
            SpanId = spanId;
            TraceId = traceId;
        }

        internal TraceId TraceId { get; }

        internal ulong SpanId { get; }
    }
}
