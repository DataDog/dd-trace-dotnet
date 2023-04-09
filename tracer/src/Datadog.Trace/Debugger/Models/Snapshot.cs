// <copyright file="Snapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Models
{
    internal record struct Snapshot
    {
        public DebuggerSnapshot Debugger { get; set; }

        public LoggerInfo Logger { get; set; }

        public int? Version { get; set; }

        public string Service { get; set; }

        public string DDSource { get; set; }

        public string DDTags { get; set; }

        public string TraceId { get; set; }

        public string SpanId { get; set; }

        public string Message { get; set; }
    }

    internal record struct LoggerInfo
    {
        public int ThreadId { get; set; }

        public string ThreadName { get; set; }

        public string Version { get; set; }

        public string Name { get; set; }

        public string Method { get; set; }
    }

    internal record struct DebuggerSnapshot
    {
        public SnapshotProbe Snapshot { get; set; }
    }
}
