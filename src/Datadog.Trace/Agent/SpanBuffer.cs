using System;
using System.Threading;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent
{
    internal class SpanBuffer
    {
        private const int HeaderSize = 5;
        private const int InitialBufferSize = 64 * 1024;

        private readonly IMessagePackFormatter<Span[]> _formatter;
        private readonly IFormatterResolver _formatterResolver;
        private readonly object _syncRoot = new object();
        private readonly int _maxBufferSize;

        private byte[] _buffer;
        private bool _locked;
        private int _offset;

        public SpanBuffer(int maxBufferSize, IFormatterResolver formatterResolver)
        {
            _maxBufferSize = maxBufferSize;
            _offset = HeaderSize;
            _buffer = new byte[InitialBufferSize];
            _formatterResolver = formatterResolver;
            _formatter = _formatterResolver.GetFormatter<Span[]>();
        }

        public ArraySegment<byte> Data => new ArraySegment<byte>(_buffer, 0, _offset);

        public int TraceCount { get; private set; }

        public int SpanCount { get; private set; }

        public bool IsFull { get; private set; }

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
                return true;
            }

            if (minDesiredSize > _maxBufferSize)
            {
                return false;
            }

            int size = _buffer.Length;

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
