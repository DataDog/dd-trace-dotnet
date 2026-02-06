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

        int SerializeSpans(ref byte[] bytes, int offset, TraceChunkModel traceChunk, int spanBufferOffset, int maxSize);

        void WriteHeader(ref byte[] bytes, int offset, int traceCount);

        int FinishBody(ref byte[] bytes, int offset, int maxSize);
    }
}
