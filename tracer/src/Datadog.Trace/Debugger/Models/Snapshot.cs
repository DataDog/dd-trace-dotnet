// <copyright file="Snapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger
{
    internal readonly record struct Snapshot
    {
        public DebuggerSnapshot Debugger { get; init; }

        public LoggerInfo Logger { get; init; }

        public int? Version { get; init; }

        public string Service { get; init; }

        public string DDSource { get; init; }

        public string DDTags { get; init; }

        public string TraceId { get; init; }

        public string SpanId { get; init; }

        public string Message { get; init; }
    }

    internal readonly record struct LoggerInfo
    {
        public int ThreadId { get; init; }

        public string ThreadName { get; init; }

        public string Version { get; init; }

        public string Name { get; init; }

        public string Method { get; init; }
    }

    internal readonly record struct DebuggerSnapshot
    {
        public ProbeSnapshot Snapshot { get; init; }
    }
}
