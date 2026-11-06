// <copyright file="MultiPartReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class MultiPartReader
    {
        // boundary is set in request ContentType
        //     multipart/form-data; boundary=uHOg3S
        // --> --uHOg3S
        private const string ContentTypePrefix = "multipart/form-data";
        private const string ContentTypeBoundary = "boundary=";
        private const int MaxBodyBytes = 1024 * 1024 * 2;
        private const int NewLibeBytesSize = 2;

#pragma warning disable SA1306 // Field names should begin with lower-case letter
        private readonly byte[] MinusBytes = Encoding.ASCII.GetBytes("-");
        private readonly byte[] NewLineBytes = { 0x0d, 0x0a };
#pragma warning restore SA1306 // Field names should begin with lower-case letter

        private readonly string _contentType;
        private readonly Encoding _encoding;
        private readonly List<MultiPartPart> _files;

        private byte[] _buffer;
        private byte[] _boundaryBytes;
        private int _boundarySize;

        private MultiPartReader(string contentType, Encoding encoding)
        {
            _contentType = contentType;
            _encoding = encoding;
            _files = new List<MultiPartPart>();
        }

        public static bool TryParse(HttpListenerRequest request, out MultiPartUpload upload)
        {
            if (request.ContentType is null || !request.ContentType.StartsWith(ContentTypePrefix))
            {
                upload = null;
                return false;
            }

            // Leave the request stream open: HttpListener still owns it.
            using var reader = new BinaryReader(request.InputStream, request.ContentEncoding, leaveOpen: true);
            var body = reader.ReadBytes(MaxBodyBytes);
            return TryParse(request.ContentType, body, request.ContentEncoding, out upload);
        }

        public static bool TryParse(string contentType, byte[] body, Encoding encoding, out MultiPartUpload upload)
        {
            upload = null;
            if (contentType is null || !contentType.StartsWith(ContentTypePrefix) || body is null || encoding is null)
            {
                return false;
            }

            var parser = new MultiPartReader(contentType, encoding);
            if (!parser.Parse(body))
            {
                return false;
            }

            upload = new MultiPartUpload(parser._files);
            return true;
        }

        private static string ExtractBoundary(string contentType)
        {
            var pos = contentType.IndexOf(ContentTypeBoundary, StringComparison.OrdinalIgnoreCase);
            if (pos == -1)
            {
                return null;
            }

            return "--" + contentType.Substring(pos + ContentTypeBoundary.Length);
        }

        private static string GetContentDispositionValue(string headerValue, string key)
        {
            var segments = headerValue.Split(';');
            foreach (var segment in segments)
            {
                var trimmed = segment.Trim();
                var separator = trimmed.IndexOf('=');
                if (separator == -1)
                {
                    continue;
                }

                var currentKey = trimmed.Substring(0, separator).Trim();
                if (!currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = trimmed.Substring(separator + 1).Trim();
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }

                return value;
            }

            return null;
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
        private bool Parse(byte[] body)
        {
            _buffer = body;

            // the boundary string pattern is stored in ContentType
            var boundary = ExtractBoundary(_contentType);
            if (boundary == null)
            {
                return false;
            }

            _boundaryBytes = _encoding.GetBytes(boundary);
            _boundarySize = _boundaryBytes.Length;

            // iterate on each file
            int pos = 0;
            while (true)
            {
                // skip boundary
                pos = IndexAfterBoundary(pos);
                if (pos == -1)
                {
                    return false;
                }

                pos += NewLibeBytesSize;

                pos = ReadHeaders(pos, out var contentType, out var name, out var filename);
                if (pos == -1)
                {
                    return false;
                }

                // file bytes start from pos

                // look for the next one (or final boundary followed by --)
                var next = IndexAfterBoundary(pos);
                if (next == -1)
                {
                    return false;
                }

                var size = next - pos - _boundarySize - NewLineBytes.Length;
                var bytes = new byte[size];
                Buffer.BlockCopy(_buffer, pos, bytes, 0, size);
                _files.Add(new MultiPartPart(contentType, name, filename, bytes, _encoding));

                // check for the last file
                if ((_buffer[next] == MinusBytes[0]) && (_buffer[next + 1] == MinusBytes[0]))
                {
                    return true;
                }
            }
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

        private int ReadHeaders(int pos, out string contentType, out string name, out string filename)
        {
            contentType = string.Empty;
            name = string.Empty;
            filename = string.Empty;

            while (true)
            {
                var next = IndexAfter(pos, NewLineBytes, NewLineBytes.Length);
                if (next == -1)
                {
                    return -1;
                }

                var line = _encoding.GetString(_buffer, pos, next - pos - NewLineBytes.Length);
                pos = next;

                // blank line ends the headers and the payload starts immediately after it
                if (string.IsNullOrEmpty(line))
                {
                    return pos;
                }

                var separator = line.IndexOf(':');
                if (separator == -1)
                {
                    continue;
                }

                var headerName = line.Substring(0, separator).Trim();
                var headerValue = line.Substring(separator + 1).Trim();

                if (headerName.Equals("content-type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = headerValue;
                    continue;
                }

                if (!headerName.Equals("content-disposition", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                name = GetContentDispositionValue(headerValue, "name") ?? name;
                filename = GetContentDispositionValue(headerValue, "filename") ?? filename;
            }
        }

        internal sealed class MultiPartUpload
        {
            private readonly List<MultiPartPart> _files;

            internal MultiPartUpload(List<MultiPartPart> files)
            {
                _files = files;
            }

            public IReadOnlyList<MultiPartPart> Files
            {
                get { return _files; }
            }

            public bool ContainsFile(string fileName)
            {
                return TryGetFile(fileName, out _);
            }

            public bool TryGetFile(string fileName, out MultiPartPart part)
            {
                foreach (var candidate in _files)
                {
                    if (candidate.FileName == fileName)
                    {
                        part = candidate;
                        return true;
                    }
                }

                part = null;
                return false;
            }
        }

        internal sealed class MultiPartPart
        {
            private readonly byte[] _bytes;
            private readonly Encoding _encoding;

            internal MultiPartPart(string contentType, string name, string fileName, byte[] bytes, Encoding encoding)
            {
                ContentType = contentType;
                Name = name;
                FileName = fileName;
                _bytes = bytes;
                _encoding = encoding;
            }

            public string ContentType { get; }

            public string Name { get; }

            public string FileName { get; }

            public byte[] ReadBytes()
            {
                var copy = new byte[_bytes.Length];
                _bytes.CopyTo(copy, 0);
                return copy;
            }

            public string ReadText()
            {
                return _encoding.GetString(_bytes);
            }

            public JsonDocument ReadJson()
            {
                return JsonDocument.Parse(ReadText());
            }
        }
    }
}
