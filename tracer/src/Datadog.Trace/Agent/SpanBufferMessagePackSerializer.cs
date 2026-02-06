// <copyright file="SpanBufferMessagePackSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent
{
    internal sealed class SpanBufferMessagePackSerializer : ISpanBufferSerializer
    {
        public const int HeaderSizeConst = 5;

        private readonly IMessagePackFormatter<TraceChunkModel> _formatter;
        private readonly IFormatterResolver _formatterResolver;

        public SpanBufferMessagePackSerializer(IFormatterResolver formatterResolver)
        {
            _formatterResolver = formatterResolver;
            _formatter = _formatterResolver.GetFormatter<TraceChunkModel>();
        }

        public int HeaderSize => HeaderSizeConst;

        public int SerializeSpans(ref byte[] bytes, int offset, TraceChunkModel traceChunk, int spanBufferOffset, int maxSize)
        {
            if (_formatter is SpanMessagePackFormatter spanFormatter)
            {
                return spanFormatter.Serialize(ref bytes, 0, in traceChunk, _formatterResolver, maxSize: maxSize);
            }
            else
            {
                return _formatter.Serialize(ref bytes, 0, traceChunk, _formatterResolver);
            }
        }

        public void WriteHeader(ref byte[] bytes, int offset, int traceCount)
        {
            MessagePackBinary.WriteArrayHeaderForceArray32Block(ref bytes, offset, (uint)traceCount);
        }

        public int FinishBody(ref byte[] bytes, int offset, int maxSize)
        {
            // No-op for MessagePack serialization
            return 0;
        }
    }
}
