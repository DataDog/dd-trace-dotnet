// <copyright file="FlowCapturedValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal readonly struct FlowCapturedValue
    {
        public FlowCapturedValue(
            ulong flowId,
            ulong frameId,
            FlowCapturePhase phase,
            FlowValueKind kind,
            int nameId,
            int typeId,
            FlowValueTag tag,
            FlowNotCapturedReason notCapturedReason,
            long numberValue,
            int stringId,
            int itemCount,
            int capturedItemCount)
        {
            FlowId = flowId;
            FrameId = frameId;
            Phase = phase;
            Kind = kind;
            NameId = nameId;
            TypeId = typeId;
            Tag = tag;
            NotCapturedReason = notCapturedReason;
            NumberValue = numberValue;
            StringId = stringId;
            ItemCount = itemCount;
            CapturedItemCount = capturedItemCount;
        }

        public ulong FlowId { get; }

        public ulong FrameId { get; }

        public FlowCapturePhase Phase { get; }

        public FlowValueKind Kind { get; }

        public int NameId { get; }

        public int TypeId { get; }

        public FlowValueTag Tag { get; }

        public FlowNotCapturedReason NotCapturedReason { get; }

        public long NumberValue { get; }

        public int StringId { get; }

        public int ItemCount { get; }

        public int CapturedItemCount { get; }
    }
}
