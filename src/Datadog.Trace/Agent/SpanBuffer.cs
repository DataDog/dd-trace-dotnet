// <copyright file="SpanBuffer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent
{
    internal class SpanBuffer
    {
        internal const int HeaderSize = 5;
        internal const int InitialBufferSize = 64 * 1024;

        private readonly IMessagePackFormatter<Span[]> _formatter;
        private readonly IFormatterResolver _formatterResolver;
        private readonly object _syncRoot = new object();
        private readonly int _maxBufferSize;

        private byte[] _buffer;
        private bool _locked;
        private int _offset;

        public SpanBuffer(int maxBufferSize, IFormatterResolver formatterResolver)
        {
            if (maxBufferSize < HeaderSize)
            {
                throw new ArgumentException(nameof(maxBufferSize), $"Buffer size should be at least {HeaderSize}");
            }

            _maxBufferSize = maxBufferSize;
            _offset = HeaderSize;
            _buffer = new byte[Math.Min(InitialBufferSize, maxBufferSize)];
            _formatterResolver = formatterResolver;
            _formatter = _formatterResolver.GetFormatter<Span[]>();
        }

        public ArraySegment<byte> Data
        {
            get
            {
                if (!_locked)
                {
                    // Sanity check - headers are written when the buffer is locked
                    throw new InvalidOperationException("Data was extracted from the buffer without locking");
                }

                return new ArraySegment<byte>(_buffer, 0, _offset);
            }
        }

        public int TraceCount { get; private set; }

        public int SpanCount { get; private set; }

        public bool IsFull { get; private set; }

        // For tests only
        internal bool IsLocked => _locked;

        // For tests only
        internal bool IsEmpty => !_locked && !IsFull && TraceCount == 0 && SpanCount == 0 && _offset == HeaderSize;

        public bool TryWrite(Span[] trace, ref byte[] temporaryBuffer)
        {
            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(_syncRoot, ref lockTaken);

                if (!lockTaken || _locked)
                {
                    // A flush operation is in progress, consider this buffer full
                    return false;
                }

                // We don't know what the serialized size of the payload will be,
                // so we need to write to a temporary buffer first
                var size = _formatter.Serialize(ref temporaryBuffer, 0, trace, _formatterResolver);

                if (!EnsureCapacity(size + _offset))
                {
                    IsFull = true;
                    return false;
                }

                Buffer.BlockCopy(temporaryBuffer, 0, _buffer, _offset, size);

                _offset += size;
                TraceCount++;
                SpanCount += trace.Length;

                return true;
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
