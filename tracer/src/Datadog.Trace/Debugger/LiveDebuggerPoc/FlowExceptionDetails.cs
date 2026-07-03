// <copyright file="FlowExceptionDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal readonly struct FlowExceptionDetails
    {
        public FlowExceptionDetails(ulong flowId, ulong frameId, int typeId, int messageId, int stackId, int hResult)
        {
            FlowId = flowId;
            FrameId = frameId;
            TypeId = typeId;
            MessageId = messageId;
            StackId = stackId;
            HResult = hResult;
        }

        public ulong FlowId { get; }

        public ulong FrameId { get; }

        public int TypeId { get; }

        public int MessageId { get; }

        public int StackId { get; }

        public int HResult { get; }
    }
}
