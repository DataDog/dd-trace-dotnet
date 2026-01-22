// <copyright file="SpanBuffer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Traces;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal sealed class SpanBuffer
    {
        internal const int HeaderSize = 5;
        internal const int InitialBufferSize = 64 * 1024;

        private readonly IMessagePackFormatter<TraceChunkModel> _formatter;
        private readonly IFormatterResolver _formatterResolver;
        private readonly TracesEncoding _encoding;
        private readonly object _syncRoot = new();
        private readonly int _maxBufferSize;

        private byte[] _buffer;
        private bool _locked;
        private int _offset;

        public SpanBuffer(int maxBufferSize, IFormatterResolver formatterResolver, TracesEncoding encoding)
        {
            if (maxBufferSize < HeaderSize)
            {
                ThrowHelper.ThrowArgumentException($"Buffer size should be at least {HeaderSize}", nameof(maxBufferSize));
            }

            _maxBufferSize = maxBufferSize;
            _offset = HeaderSize;
            _buffer = new byte[Math.Min(InitialBufferSize, maxBufferSize)];
            _formatterResolver = formatterResolver;
            _formatter = _formatterResolver.GetFormatter<TraceChunkModel>();
            _encoding = encoding;
        }

        public enum WriteStatus
        {
            Success = 0,
            Full = 1,
            Overflow = 2
        }

        public ArraySegment<byte> Data
        {
            get
            {
                if (!_locked)
                {
                    // Sanity check - headers are written when the buffer is locked
                    ThrowHelper.ThrowInvalidOperationException("Data was extracted from the buffer without locking");
                }

                return new ArraySegment<byte>(_buffer, 0, _offset);
                // return new ArraySegment<byte>(_buffer, _offset, 0); // The constructor should be array, offet, count
            }
        }

        public int TraceCount { get; private set; }

        public int SpanCount { get; private set; }

        public bool IsFull { get; private set; }

        // For tests only
        internal bool IsLocked => _locked;

        // For tests only
        internal bool IsEmpty => !_locked && !IsFull && TraceCount == 0 && SpanCount == 0 && _offset == HeaderSize;

        public WriteStatus TryWrite(in SpanCollection spans, ref byte[] temporaryBuffer, int? samplingPriority = null)
        {
            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(_syncRoot, ref lockTaken);

                if (!lockTaken || _locked)
                {
                    // A flush operation is in progress, consider this buffer full
                    return WriteStatus.Full;
                }

                // We don't know what the serialized size of the payload will be,
                // so we need to write to a temporary buffer first
                int size = 0;

                // TODO: Encode spans as OTLP protobuf or OTLP JSON
                var traceChunk = new TraceChunkModel(in spans, samplingPriority, isFirstChunkInPayload: TraceCount == 0);
                if (_encoding == TracesEncoding.DatadogV0)
                {
                    // since all we have is an array of spans, use the trace context from the first span
                    // to get the other values we need (sampling priority, origin, trace tags, etc) for now.
                    // the idea is that as we refactor further, we can pass more than just the spans,
                    // and these values can come directly from the trace context.

                    if (_formatter is SpanMessagePackFormatter spanFormatter)
                    {
                        size = spanFormatter.Serialize(ref temporaryBuffer, 0, in traceChunk, _formatterResolver, maxSize: _maxBufferSize);
                    }
                    else
                    {
                        size = _formatter.Serialize(ref temporaryBuffer, 0, traceChunk, _formatterResolver);
                    }
                }
                else if (_encoding == TracesEncoding.OtlpJson)
                {
                    // All spans will have the same Resource
                    // Now we create a list of SccopeSpans which differs based on the InstrumentationScope
                    // For now, let's assume all spans come from the same InstrumentationScope
                    // TODO: Convert trace chunk to TracesData
                    size = OtlpTracesSerializer.SerializeToJson(ref temporaryBuffer, 0, in traceChunk, maxSize: _maxBufferSize);
                }

                if (size == 0)
                {
                    // Serialization failed because the trace is too big
                    return WriteStatus.Overflow;
                }

                if (!EnsureCapacity(size + _offset))
                {
                    IsFull = true;
                    return WriteStatus.Full;
                }

                Buffer.BlockCopy(temporaryBuffer, 0, _buffer, _offset, size);

                _offset += size;
                TraceCount++;
                SpanCount += spans.Count;

                return WriteStatus.Success;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_syncRoot);
                }
            }
        }

        public bool Lock()
        {
            lock (_syncRoot)
            {
                if (_locked)
                {
                    return false;
                }

                // Use a fixed-size header
                MessagePackBinary.WriteArrayHeaderForceArray32Block(ref _buffer, 0, (uint)TraceCount);
                _locked = true;

                return true;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _offset = HeaderSize;
                TraceCount = 0;
                SpanCount = 0;
                IsFull = false;
                _locked = false;
            }
        }

        private bool EnsureCapacity(int minDesiredSize)
        {
            if (minDesiredSize <= _buffer.Length)
            {
                // The buffer is already big enough
                return true;
            }

            if (minDesiredSize > _maxBufferSize)
            {
                // Trying to write more than the allowed limit
                return false;
            }

            int size = _buffer.Length;

            // Double the size of the buffer until it's big enough
            while (size < minDesiredSize && size < _maxBufferSize)
            {
                size *= 2;
            }

            if (size > _maxBufferSize)
            {
                size = _maxBufferSize;
            }

            var newBuffer = new byte[size];

            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _offset);

            _buffer = newBuffer;

            return true;
        }
    }
}
