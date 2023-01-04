// <copyright file="MultiPartReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class MultiPartReader
    {
        // boundary is set in request ContentType
        //     multipart/form-data; boundary=uHOg3S
        // --> --uHOg3S
        private const string ContentTypeBoundary = "boundary=";
        private const int NewLibeBytesSize = 2;

        // the request encoding seems to be ASCII for these parts
#pragma warning disable SA1306 // Field names should begin with lower-case letter
        private readonly byte[] ContentTypeBytes = Encoding.ASCII.GetBytes("content-type: ");
        private readonly byte[] NameBytes = Encoding.ASCII.GetBytes("name=");
        private readonly byte[] FilenameBytes = Encoding.ASCII.GetBytes("filename=");
        private readonly byte[] QuoteBytes = Encoding.ASCII.GetBytes("\"");
        private readonly byte[] MinusBytes = Encoding.ASCII.GetBytes("-");
        private readonly byte[] NewLineBytes = { 0x0d, 0x0a };
#pragma warning restore SA1306 // Field names should begin with lower-case letter

        private HttpListenerRequest _request;
        private byte[] _buffer;
        private byte[] _boundaryBytes;
        private int _boundarySize;

        private List<MultiPartFileInfo> _files;

        public MultiPartReader(HttpListenerRequest request)
        {
            _request = request;
            _files = new List<MultiPartFileInfo>();
        }

        public List<MultiPartFileInfo> Files
        {
            get
            {
                return _files;
            }
        }

        public string GetStringFile(int start, int size)
        {
            if ((start + size) > _buffer.Length)
            {
                return string.Empty;
            }

            return _request.ContentEncoding.GetString(_buffer, start, size);
        }

        // The different files are separated by a "boundary" specified in the request ContentType
        //
        //  --boundary\r\n
        //  content-type: application/json\r\n
        //  content-disposition: form-data; name="event"; filename="event.json"\r\n
        //  \r\n
        //   ...
        //  \r\n--boundary\r\n
        //  content-type: application/octet-stream\r\n
        //  content-disposition: form-data; name="auto.pprof"; filename="auto.pprof"\r\n
        //  \r\n
        //  ...
        //  \r\n--boundary\r\n
        //  content-type: application/octet-stream\r\n
        //  content-disposition: form-data; name="metrics.json"; filename="metrics.json"\r\n
        //  \r\n
        //  ...
        //  \r\n--boundary--
        //
        // Since .pprof are binary files, it is needed to parse the body as binary
        // The .json file encoding is given by the request ContentEncoding
        public bool Parse()
        {
            // the boundary string pattern is stored in ContentType
            var boundary = ExtractBoundary(_request.ContentType);
            if (boundary == null)
            {
                return false;
            }

            _boundaryBytes = _request.ContentEncoding.GetBytes(boundary);
            _boundarySize = _boundaryBytes.Length;

            // read the entire body (should not be too big in tests)
            // _request.ContentLength64 is -1 so use a fixed size
            var reader = new BinaryReader(_request.InputStream);
            _buffer = reader.ReadBytes(1024 * 1024 * 2);

            // iterate on each file
            int pos = 0;
            while (true)
            {
                // look for file info
                string contentType;
                string name;
                string filename;

                // skip boundary
                pos = IndexAfterBoundary(pos);
                if (pos == -1)
                {
                    return false;
                }

                pos += NewLibeBytesSize;

                pos = GetContentType(pos, out contentType);
                if (pos == -1)
                {
                    return false;
                }

                pos = GetKey(pos, NameBytes, out name);
                if (pos == -1)
                {
                    return false;
                }

                pos = GetKey(pos, FilenameBytes, out filename);
                if (pos == -1)
                {
                    return false;
                }

                // skip 2 new line
                pos += 2 * NewLineBytes.Length;

                // file bytes start from pos

                // look for the next one (or final boundary followed by --)
                var next = IndexAfterBoundary(pos);
                if (next == -1)
                {
                    return false;
                }

                _files.Add(new MultiPartFileInfo()
                {
                    ContentType = contentType,
                    Name = name,
                    FileName = filename,
                    BytesPos = pos,
                    BytesSize = next - pos - _boundarySize - NewLineBytes.Length
                });

                // check for the last file
                if ((_buffer[next] == MinusBytes[0]) && (_buffer[next + 1] == MinusBytes[0]))
                {
                    return true;
                }
            }
        }

        private static string ExtractBoundary(string contentType)
        {
            var pos = contentType.IndexOf(ContentTypeBoundary);
            if (pos == -1)
            {
                return null;
            }

            return "--" + contentType.Substring(pos + ContentTypeBoundary.Length);
        }

        // return the position in _buffer AFTER the given array of bytes
        private int IndexAfter(int pos, byte[] bytes, int size)
        {
            int current = 0;
            while (pos < _buffer.Length)
            {
                if (_buffer[pos] == bytes[current])
                {
                    current++;

                    if (current == size)
                    {
                        return pos + 1;
                    }
                }
                else
                {
                    current = 0;
                }

                pos++;
            }

            return -1;
        }

        private int IndexAfterBoundary(int pos)
        {
            return IndexAfter(pos, _boundaryBytes, _boundarySize);
        }

        private int GetContentType(int pos, out string contentType)
        {
            contentType = string.Empty;

            pos = IndexAfter(pos, ContentTypeBytes, ContentTypeBytes.Length);
            if (pos == -1)
            {
                return -1;
            }

            int next = IndexAfter(pos, NewLineBytes, NewLineBytes.Length);
            if (next == -1)
            {
                return -1;
            }

            contentType = _request.ContentEncoding.GetString(_buffer, pos, next - pos - NewLineBytes.Length);

            return next;
        }

        // look for key="value" such as
        //    name="event"; filename="event.json"
        private int GetKey(int pos, byte[] keyBytes, out string value)
        {
            value = string.Empty;

            pos = IndexAfter(pos, keyBytes, keyBytes.Length);
            if (pos == -1)
            {
                return -1;
            }

            // skip "
            pos += QuoteBytes.Length;

            // look for the closing "
            int next = IndexAfter(pos, QuoteBytes, QuoteBytes.Length);
            if (next == -1)
            {
                return -1;
            }

            value = _request.ContentEncoding.GetString(_buffer, pos, next - pos - QuoteBytes.Length);

            return next;
        }

        internal class MultiPartFileInfo
        {
            public string ContentType { get; set; }

            public string Name { get; set; }

            public string FileName { get; set; }

            public int BytesPos { get; set; }

            public int BytesSize { get; set; }
        }
    }
}
