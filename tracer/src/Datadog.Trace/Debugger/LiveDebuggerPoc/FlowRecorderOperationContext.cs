// <copyright file="FlowRecorderOperationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal sealed class FlowRecorderOperationContext
    {
        private int _active = 1;

        public FlowRecorderOperationContext(long generation, ulong operationId, ulong traceIdUpper, ulong traceIdLower, ulong rootSpanId, ulong activeSpanId)
        {
            Generation = generation;
            OperationId = operationId;
            TraceIdUpper = traceIdUpper;
            TraceIdLower = traceIdLower;
            RootSpanId = rootSpanId;
            ActiveSpanId = activeSpanId;
        }

        public long Generation { get; }

        public ulong OperationId { get; }

        public ulong TraceIdUpper { get; }

        public ulong TraceIdLower { get; }

        public ulong RootSpanId { get; }

        public ulong ActiveSpanId { get; }

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public bool TryDeactivate()
        {
            return Interlocked.Exchange(ref _active, 0) != 0;
        }
    }
}
