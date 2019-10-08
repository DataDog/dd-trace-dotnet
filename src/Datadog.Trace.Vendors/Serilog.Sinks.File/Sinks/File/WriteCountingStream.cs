// Copyright 2013-2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;

namespace Datadog.Trace.Vendors.Serilog.Sinks.File
{
    sealed class WriteCountingStream : Stream
    {
        readonly Stream _stream;
        long _countedLength;

        public WriteCountingStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _stream = stream;
            _countedLength = stream.Length;
        }

        public long CountedLength => _countedLength;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _stream.Dispose();

            base.Dispose(disposing);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _countedLength += count;
        }

        public override void Flush() => _stream.Flush();
        public override bool CanRead => false;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => true;
        public override long Length => _stream.Length;


        public override long Position
        {
            get { return _stream.Position; }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException($"Seek operations are not available through `{nameof(WriteCountingStream)}`.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}