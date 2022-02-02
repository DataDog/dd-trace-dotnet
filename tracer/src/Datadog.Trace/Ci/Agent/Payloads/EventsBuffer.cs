// <copyright file="EventsBuffer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class EventsBuffer<T>
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<EventsBuffer<T>>();

        internal const int HeaderSize = 5;
        internal const int InitialBufferSize = 64 * 1024;

        private readonly IMessagePackFormatter<T> _formatter;
        private readonly IFormatterResolver _formatterResolver;
        private readonly object _syncRoot = new object();
        private readonly int _maxBufferSize;

        private byte[] _buffer;
        private bool _locked;
        private int _offset;

        public EventsBuffer(int maxBufferSize, IFormatterResolver formatterResolver)
        {
            if (maxBufferSize < HeaderSize)
            {
                ThrowHelper.ThrowArgumentException($"Buffer size should be at least {HeaderSize}", nameof(maxBufferSize));
            }

            _maxBufferSize = maxBufferSize;
            _offset = HeaderSize;
            _buffer = new byte[Math.Min(InitialBufferSize, maxBufferSize)];
            _formatterResolver = formatterResolver;
            _formatter = _formatterResolver.GetFormatter<T>();

            if (_formatter is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Formatter for '{_formatter}' is null");
            }
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
            }
        }

        public int Count { get; private set; }

        public bool IsFull { get; private set; }

        // For tests only
        internal bool IsLocked => _locked;

        // For tests only
        internal bool IsEmpty => !_locked && !IsFull && Count == 0 && _offset == HeaderSize;

        public bool TryWrite(T item)
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

                if (IsFull)
                {
                    // Buffer is full
                    return false;
                }

                try
                {
                    // We serialize the item
                    // Note: By serializing an item near the buffer limit can make the buffer to grow
                    // and at the same time we will reject that item. Although we don't expect that happens
                    // too often.
                    var size = _formatter.Serialize(ref _buffer, _offset, item, _formatterResolver);

                    // In case we overpass the max buffer size, we reject the last serialization.
                    if (_offset + size > _maxBufferSize)
                    {
                        return false;
                    }

                    // Move the offset to accept the last serialization and increase the counter
                    _offset += size;
                    Count++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }

                if (_offset == _maxBufferSize)
                {
                    IsFull = true;
                }

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
                MessagePackBinary.WriteArrayHeaderForceArray32Block(ref _buffer, 0, (uint)Count);
                _locked = true;

                return true;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _offset = HeaderSize;
                Count = 0;
                IsFull = false;
                _locked = false;
            }
        }
    }
}
