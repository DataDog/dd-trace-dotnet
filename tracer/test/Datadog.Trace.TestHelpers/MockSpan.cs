// <copyright file="MockSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using MessagePack; // use nuget MessagePack to deserialize

namespace Datadog.Trace.TestHelpers
{
    [MessagePackObject]
    [DebuggerDisplay("{ToString(),nq}")]
    public class MockSpan
    {
        [Key("trace_id")]
        public ulong TraceId { get; set; }

        [Key("span_id")]
        public ulong SpanId { get; set; }

        [Key("name")]
        public string OperationName { get; set; }

        [Key("resource")]
        public string ResourceName { get; set; }

        [Key("service")]
        public string ServiceName { get; set; }

        [Key("type")]
        public string Type { get; set; }

        [Key("start")]
        public long Start { get; set; }

        [Key("duration")]
        public long Duration { get; set; }

        [Key("parent_id")]
        public ulong? ParentId { get; set; }

        [Key("error")]
        public byte Error { get; set; }

        [Key("meta")]
        public Dictionary<string, string> Tags { get; set; }

        [Key("metrics")]
        public Dictionary<string, double> Metrics { get; set; }

        public override string ToString()
        {
            return $"{nameof(TraceId)}: {TraceId}, {nameof(SpanId)}: {SpanId}, {nameof(OperationName)}: {OperationName}, {nameof(ResourceName)}: {ResourceName}, {nameof(ServiceName)}: {ServiceName}";
        }
    }
}
