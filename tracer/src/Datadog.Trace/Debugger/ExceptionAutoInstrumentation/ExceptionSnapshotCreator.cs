// <copyright file="ExceptionSnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionSnapshotCreator : IDebuggerSnapshotCreator
    {
        public ExceptionSnapshotCreator(ExceptionProbeProcessor[] processors, string probeId)
        {
            Processors = processors;
            ProbeId = probeId;
        }

        public string ProbeId { get; }

        public ExceptionProbeProcessor[] Processors { get; }

        public uint EnterHash { get; set; }

        public uint LeaveHash { get; set; }

        public TrackedStackFrameNode TrackedStackFrameNode { get; set; }
    }
}
