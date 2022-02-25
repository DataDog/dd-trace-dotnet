// <copyright file="WriteOnlyStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading;
using Datadog.Util;

namespace Datadog.Profiler
{
    internal class WriteOnlyStream : Stream
    {
        private const string ObjectDisposedMessage = "This " + nameof(WriteOnlyStream) + " is already disposed.";

        private readonly bool _leaveUnderlyingStreamOpenWhenDisposed;
        private Stream _underlyingStream;
        private long _writtenBytes;

        public WriteOnlyStream(Stream underlyingStream, bool leaveUnderlyingStreamOpenWhenDisposed)
        {
            Validate.NotNull(underlyingStream, nameof(underlyingStream));

            if (!underlyingStream.CanWrite)
            {
                throw new ArgumentException($"The specified {nameof(underlyingStream)} must support"
                                           + " writing, but its CanWrite property returned false.");
            }

            _leaveUnderlyingStreamOpenWhenDisposed = leaveUnderlyingStreamOpenWhenDisposed;
            _underlyingStream = underlyingStream;
            _writtenBytes = 0;
        }

        public bool LeaveUnderlyingStreamOpenWhenDisposed
        {
            get { return _leaveUnderlyingStreamOpenWhenDisposed; }
        }

        public long WrittenBytes
        {
            get { return _writtenBytes; }
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Stream underlyingStream = _underlyingStream ?? throw new ObjectDisposedException(ObjectDisposedMessage);
            underlyingStream.Write(buffer, offset, count);
            _writtenBytes += count;
        }

        public override void Flush()
        {
            Stream underlyingStream = _underlyingStream ?? throw new ObjectDisposedException(ObjectDisposedMessage);
            underlyingStream.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            Stream underlyingStream = Interlocked.Exchange(ref _underlyingStream, null);

            if (underlyingStream != null)
            {
                underlyingStream.Flush();

                if (!_leaveUnderlyingStreamOpenWhenDisposed)
                {
                    underlyingStream.Dispose();
                }
            }
        }
    }
}
