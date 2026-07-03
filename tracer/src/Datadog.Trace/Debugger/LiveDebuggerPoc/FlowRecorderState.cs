// <copyright file="FlowRecorderState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    /// <summary>
    /// Opaque state returned by the live debugger POC recorder enter callback.
    /// </summary>
    public readonly struct FlowRecorderState
    {
        internal FlowRecorderState(long generation, FlowRecorderOperationContext? operationContext, ulong operationId, ulong flowId, ulong frameId, ulong parentFrameId, int depth, int methodMetadataIndex, ulong previousAsyncOperationId = 0, long previousAsyncOperationGeneration = 0, bool restoreAsyncOperationId = false)
        {
            Generation = generation;
            OperationContext = operationContext;
            OperationId = operationId;
            FlowId = flowId;
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            Depth = depth;
            MethodMetadataIndex = methodMetadataIndex;
            PreviousAsyncOperationId = previousAsyncOperationId;
            PreviousAsyncOperationGeneration = previousAsyncOperationGeneration;
            RestoreAsyncOperationId = restoreAsyncOperationId;
        }

        internal long Generation { get; }

        internal FlowRecorderOperationContext? OperationContext { get; }

        internal ulong OperationId { get; }

        internal ulong FlowId { get; }

        internal ulong FrameId { get; }

        internal ulong ParentFrameId { get; }

        internal int Depth { get; }

        internal int MethodMetadataIndex { get; }

        internal ulong PreviousAsyncOperationId { get; }

        internal long PreviousAsyncOperationGeneration { get; }

        internal bool RestoreAsyncOperationId { get; }

        internal bool IsValid => FlowId != 0 && FrameId != 0;
    }
}
