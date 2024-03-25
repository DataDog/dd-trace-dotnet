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
    public class MockSpanLink
    {
        [Key("trace_id")]
        public ulong TraceIdLow { get; set; }

        [Key("trace_id_high")]
        public ulong TraceIdHigh { get; set; }

        [Key("span_id")]
        public ulong SpanId { get; set; }
    }
}
