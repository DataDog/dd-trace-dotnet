// <copyright file="FlowEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal readonly struct FlowEvent
    {
        public const int BinarySize =
            sizeof(byte) +
            (sizeof(long) * 2) +
            (sizeof(int) * 3) +
            (sizeof(ulong) * 7);

        public FlowEvent(
            FlowEventKind kind,
            long timestamp,
            int methodMetadataIndex,
            ulong flowId,
            ulong frameId,
            ulong parentFrameId,
            int depth,
            int threadId,
            ulong traceIdUpper,
            ulong traceIdLower,
            ulong rootSpanId,
            ulong activeSpanId,
            long exceptionTypeId)
        {
            Kind = kind;
            Timestamp = timestamp;
            MethodMetadataIndex = methodMetadataIndex;
            FlowId = flowId;
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            Depth = depth;
            ThreadId = threadId;
            TraceIdUpper = traceIdUpper;
            TraceIdLower = traceIdLower;
            RootSpanId = rootSpanId;
            ActiveSpanId = activeSpanId;
            ExceptionTypeId = exceptionTypeId;
        }

        public FlowEventKind Kind { get; }

        public long Timestamp { get; }

        public int MethodMetadataIndex { get; }

        public ulong FlowId { get; }

        public ulong FrameId { get; }

        public ulong ParentFrameId { get; }

        public int Depth { get; }

        public int ThreadId { get; }

        public ulong TraceIdUpper { get; }

        public ulong TraceIdLower { get; }

        public ulong RootSpanId { get; }

        public ulong ActiveSpanId { get; }

        public long ExceptionTypeId { get; }
    }
}
