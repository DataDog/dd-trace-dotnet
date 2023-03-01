// <copyright file="SpanMutated.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// A SpanMutated represents a thing
    /// </summary>
    internal class SpanMutated
    {
        // pub struct Span {
        //     service: Option<String>,
        //     name: String,
        //     resource: String,
        //     trace_id: u64,
        //     span_id: u64,
        //     parent_id: Option<u64>,
        //     start: i64,
        //     duration: i64,
        //     error: i32,
        // }
        public string Service { get; set; }

        public string Name { get; set; }

        public string Resource { get; set; }

        public ulong Trace_id { get; set; }

        public ulong Span_id { get; set; }

        public ulong Parent_id { get; set; }

        public long Start { get; set; }

        public long Duration { get; set; }

        public int Error { get; set; }
    }
}
