using System;
using System.Threading;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent
{
    internal class SpanBuffer
    {
        private const int HeaderSize = 5;

        private readonly IMessagePackFormatter<Span[]> _formatter;
        private readonly IFormatterResolver _formatterResolver;
        private readonly object _syncRoot = new object();

        private byte[] _buffer;
        private bool _locked;
        private int _offset;

        public SpanBuffer(int bufferSize, IFormatterResolver formatterResolver)
        {
            _offset = HeaderSize;
            _buffer = new byte[bufferSize];
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

                if (size + _offset >= _buffer.Length)
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
    }
}
