// <copyright file="FlowRecorderState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    /// <summary>
    /// Opaque state returned by the live debugger POC recorder enter callback.
    /// </summary>
    public readonly struct FlowRecorderState
    {
        internal FlowRecorderState(long generation, ulong flowId, ulong frameId, ulong parentFrameId, int depth, int methodMetadataIndex)
        {
            Generation = generation;
            FlowId = flowId;
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            Depth = depth;
            MethodMetadataIndex = methodMetadataIndex;
        }

        internal long Generation { get; }

        internal ulong FlowId { get; }

        internal ulong FrameId { get; }

        internal ulong ParentFrameId { get; }

        internal int Depth { get; }

        internal int MethodMetadataIndex { get; }

        internal bool IsValid => FlowId != 0 && FrameId != 0;
    }
}
