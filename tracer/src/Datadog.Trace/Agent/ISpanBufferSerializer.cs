// <copyright file="ISpanBufferSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent
{
    internal interface ISpanBufferSerializer
    {
        int HeaderSize { get; }

        /// <summary>
        /// Gets the number of bytes <see cref="FinishBody"/> may append when the payload is
        /// finalized. <see cref="SpanBuffer"/> reserves this much room on every write, so that
        /// finalizing can never need to grow the buffer - it runs while the buffer's lock is held,
        /// and that critical section must stay allocation-free.
        /// </summary>
        int TrailerSize { get; }

        int SerializeSpans(ref byte[] bytes, int temporaryBufferOffset, TraceChunkModel traceChunk, int spanBufferOffset, int maxSize);

        void WriteHeader(ref byte[] bytes, int offset, int traceCount);

        int FinishBody(ref byte[] bytes, int offset, int maxSize);
    }
}
