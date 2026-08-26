// <copyright file="SpanBuffer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent
{
    internal sealed class SpanBuffer
    {
        internal const int InitialBufferSize = 64 * 1024;

        private readonly ISpanBufferSerializer _serializer;
        private readonly object _syncRoot = new();
        private readonly int _maxBufferSize;

        private byte[] _buffer;
        private int _offset;

        public SpanBuffer(int maxBufferSize, ISpanBufferSerializer serializer)
        {
            var minimumSize = serializer.HeaderSize + serializer.TrailerSize;

            if (maxBufferSize < minimumSize)
            {
                ThrowHelper.ThrowArgumentException($"Buffer size should be at least {minimumSize}", nameof(maxBufferSize));
            }

            _maxBufferSize = maxBufferSize;
            _offset = serializer.HeaderSize;
            _buffer = new byte[Math.Min(InitialBufferSize, maxBufferSize)];
            _serializer = serializer;
        }

        public enum WriteStatus
        {
            Success = 0,
            Full = 1,
            Overflow = 2,
            Locked = 3
        }

        public int TraceCount { get; private set; }

        public int SpanCount { get; private set; }

        public bool IsFull { get; private set; }

        internal int MaxBufferSize => _maxBufferSize;

        /// <summary>
        /// Gets the length of the array currently backing the buffer, so that callers of
        /// <see cref="Detach"/> can size the replacement array without holding the lock.
        /// Read without synchronization: the serialization thread may replace the array at any
        /// time, but both the old and the new reference are valid arrays, so the worst case is a
        /// replacement that is one growth generation behind and grows on its next write.
        /// </summary>
        internal int CurrentLength => _buffer.Length;

        /// <summary>
        /// Gets the raw contents of the buffer, without finalizing the payload. Only useful for
        /// asserting on what a write did or didn't put in the buffer; production code takes the
        /// payload from <see cref="Detach"/> instead.
        /// </summary>
        [TestingOnly]
        internal ArraySegment<byte> RawData => new(_buffer, 0, _offset);

        [TestingOnly]
        internal bool IsEmpty => !IsFull && TraceCount == 0 && SpanCount == 0 && _offset == _serializer.HeaderSize;

        public WriteStatus TryWrite(in SpanCollection spans, ref byte[] temporaryBuffer, int? samplingPriority = null)
        {
            bool lockTaken = false;

            try
            {
                // Wait, rather than giving up immediately as this used to. Flushing doesn't
                // hold the buffer across a network send, so contention should be very small
                // and waiting it out costs nothing. The bounded timeout only exists so
                // that a pathological holder can never stall the serialization thread for good.
                const int lockTimeoutMs = 100;
                Monitor.TryEnter(_syncRoot, lockTimeoutMs, ref lockTaken);

                if (!lockTaken)
                {
                    // This should be very rare, and only happen in pathological/overload cases where
                    // the flushing thread is rescheduled in the middle of the lock()
                    return WriteStatus.Locked;
                }

                // since all we have is an array of spans, use the trace context from the first span
                // to get the other values we need (sampling priority, origin, trace tags, etc) for now.
                // the idea is that as we refactor further, we can pass more than just the spans,
                // and these values can come directly from the trace context.
                var traceChunk = new TraceChunkModel(in spans, samplingPriority, isFirstChunkInPayload: TraceCount == 0);

                // We don't know what the serialized size of the payload will be,
                // so we need to write to a temporary buffer first
                int size = _serializer.SerializeSpans(ref temporaryBuffer, 0, traceChunk, _offset, maxSize: _maxBufferSize);

                if (size == 0)
                {
                    // Serialization failed because the trace is too big
                    return WriteStatus.Overflow;
                }

                // Reserve room for whatever FinishBody will append, which ensures
                // the payload in Detach doesn't grow the array while the lock is held.
                if (!EnsureCapacity(size + _offset + _serializer.TrailerSize))
                {
                    if (TraceCount == 0)
                    {
                        // The trace cannot fit in an empty buffer
                        return WriteStatus.Overflow;
                    }

                    IsFull = true;
                    return WriteStatus.Full;
                }

                Buffer.BlockCopy(temporaryBuffer, 0, _buffer, _offset, size);

                _offset += size;
                TraceCount++;
                SpanCount += traceChunk.SpanCount;

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

        /// <summary>
        /// Finalizes the current payload and hands it to the caller, swapping
        /// <paramref name="replacement"/> in as the new backing array so that the buffer is
        /// immediately writable again, minimizing the duration the buffer is locked.
        /// </summary>
        /// <param name="replacement">
        /// The array to write into from now on. Only consumed if there was something to detach.
        /// Must not be the array currently backing the buffer.
        /// </param>
        /// <returns>
        /// The detached payload, or <c>default</c> if the buffer held no traces, in which case
        /// <paramref name="replacement"/> is unused.
        /// </returns>
        public Payload Detach(byte[] replacement)
        {
            lock (_syncRoot)
            {
                if (TraceCount == 0)
                {
                    // Nothing to send, so don't consume the caller's replacement array
                    return default;
                }

                // Use a fixed-size header
                _serializer.WriteHeader(ref _buffer, 0, TraceCount);
                int addedBytes = _serializer.FinishBody(ref _buffer, _offset, _maxBufferSize);
                _offset += addedBytes;

                var payload = new Payload(new ArraySegment<byte>(_buffer, 0, count: _offset), TraceCount, SpanCount);

                _buffer = replacement;
                _offset = _serializer.HeaderSize;
                TraceCount = 0;
                SpanCount = 0;
                IsFull = false;

                return payload;
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

        public readonly struct Payload
        {
            public readonly ArraySegment<byte> Data;
            public readonly int TraceCount;
            public readonly int SpanCount;

            public Payload(ArraySegment<byte> data, int traceCount, int spanCount)
            {
                Data = data;
                TraceCount = traceCount;
                SpanCount = spanCount;
            }

            /// <summary>
            /// Gets the detached array, or <c>null</c> if there was nothing to detach.
            /// </summary>
            public byte[]? Array => Data.Array;
        }
    }
}
