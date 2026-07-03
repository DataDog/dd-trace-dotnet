// <copyright file="FlowOperationMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal readonly struct FlowOperationMetadata
    {
        public FlowOperationMetadata(
            ulong operationId,
            long generation,
            string triggerReason,
            string root,
            long startTimestamp,
            ulong traceIdUpper,
            ulong traceIdLower,
            ulong rootSpanId,
            ulong activeSpanId)
        {
            OperationId = operationId;
            Generation = generation;
            TriggerReason = triggerReason;
            Root = root;
            StartTimestamp = startTimestamp;
            TraceIdUpper = traceIdUpper;
            TraceIdLower = traceIdLower;
            RootSpanId = rootSpanId;
            ActiveSpanId = activeSpanId;
        }

        public ulong OperationId { get; }

        public long Generation { get; }

        public string TriggerReason { get; }

        public string Root { get; }

        public long StartTimestamp { get; }

        public ulong TraceIdUpper { get; }

        public ulong TraceIdLower { get; }

        public ulong RootSpanId { get; }

        public ulong ActiveSpanId { get; }
    }
}
