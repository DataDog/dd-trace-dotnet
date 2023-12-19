// <copyright file="MockHttpParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Streams;

namespace Datadog.Trace.TestHelpers
{
    internal class MockHttpParser
    {
        private const string ContentLengthHeaderKey = "Content-Length";

        public static async Task<MockHttpRequest> ReadRequest(Stream stream)
        {
            var headers = new HttpHeaders();
            var helper = new StreamReaderHelper(stream);

            var stringBuilder = new StringBuilder();

            // Read POST
            await helper.ReadUntil(stringBuilder, stopChar: ' ').ConfigureAwait(false);

            // This will have a null character prefixed on it because of reasons
            var method = stringBuilder.ToString(1, stringBuilder.Length - 1);
            stringBuilder.Clear();

            // Read /path?request
            await helper.GoNextChar().ConfigureAwait(false);
            await helper.ReadUntil(stringBuilder, stopChar: ' ').ConfigureAwait(false);

            var pathAndQuery = stringBuilder.ToString().Trim();
            stringBuilder.Clear();

            // Skip to end of line
            await helper.ReadUntilNewLine(stringBuilder).ConfigureAwait(false);
            stringBuilder.Clear();

            // Read headers
            do
            {
                await helper.GoNextChar().ConfigureAwait(false);

                // Check for end of headers
                if (await helper.IsNewLine().ConfigureAwait(false))
                {
                    // Empty line, content starts next
                    break;
                }

                // Read key
                await helper.ReadUntil(stringBuilder, stopChar: ':').ConfigureAwait(false);

                var name = stringBuilder.ToString().Trim();
                stringBuilder.Clear();

                // skip separator
                await helper.GoNextChar().ConfigureAwait(false);

                // Read value
                await helper.ReadUntilNewLine(stringBuilder).ConfigureAwait(false);

                var value = stringBuilder.ToString().Trim();
                stringBuilder.Clear();

                headers.Add(name, value);
            }
            while (true);

            var length = long.TryParse(headers.GetValue(ContentLengthHeaderKey), out var headerValue) ? headerValue : (long?)null;
            var body = headers.GetValue("Transfer-Encoding") is "chunked"
                           ? new ChunkedEncodingReadContent(stream)
                           : new StreamContent(stream, length);

            return new MockHttpRequest()
            {
                Headers = headers,
                Method = method,
                PathAndQuery = pathAndQuery,
                ContentLength = length,
                Body = body
            };
        }

        internal class MockHttpRequest
        {
            public HttpHeaders Headers { get; set; } = new HttpHeaders();

            public string Method { get; set; }

            public string PathAndQuery { get; set; }

            public long? ContentLength { get; set; }

            public StreamContent Body { get; set; }

            public static MockHttpRequest Create(HttpListenerRequest request)
            {
                var headers = new HttpHeaders(request.Headers.Count);

                foreach (var key in request.Headers.AllKeys)
                {
                    foreach (var value in request.Headers.GetValues(key))
                    {
                        headers.Add(key, value);
                    }
                }

                return new MockHttpRequest
                {
                    Headers = headers,
                    Method = request.HttpMethod,
                    PathAndQuery = request.Url?.PathAndQuery,
                    ContentLength = request.ContentLength64,
                    Body = new StreamContent(request.InputStream, request.ContentLength64),
                };
            }
        }

        private class StreamReaderHelper(Stream stream)
        {
            // https://tools.ietf.org/html/rfc2616#section-4.2
            private const int BufferSize = 10;
            private readonly byte[] _chArray = new byte[BufferSize];
            private readonly Stream _stream = stream;

            private char _currentChar = char.MinValue;
            private int _streamPosition = 0;

            public async Task GoNextChar()
            {
                var bytesRead = await _stream.ReadAsync(_chArray, offset: 0, count: 1).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    ThrowHelper.ThrowInvalidOperationException($"Unexpected end of stream at position {_streamPosition}");
                }

                _currentChar = Encoding.ASCII.GetChars(_chArray)[0];
                _streamPosition++;
            }

            public async Task ReadUntil(StringBuilder builder, char stopChar)
            {
                while (!_currentChar.Equals(stopChar))
                {
                    builder.Append(_currentChar);
                    await GoNextChar().ConfigureAwait(false);
                }
            }

            public async Task ReadUntilNewLine(StringBuilder builder)
            {
                do
                {
                    if (await IsNewLine().ConfigureAwait(false))
                    {
                        break;
                    }

                    await ReadUntil(builder, DatadogHttpValues.CarriageReturn).ConfigureAwait(false);
                }
                while (true);
            }

            public async Task<bool> IsNewLine()
            {
                if (_currentChar.Equals(DatadogHttpValues.CarriageReturn))
                {
                    // end of headers
                    // Next character should be a LineFeed, regardless of Linux/Windows
                    // Skip the newline indicator
                    await GoNextChar().ConfigureAwait(false);

                    if (!_currentChar.Equals(DatadogHttpValues.LineFeed))
                    {
                        ThrowHelper.ThrowException($"Unexpected character {_currentChar} in headers: CR must be followed by LF");
                    }

                    return true;
                }

                return false;
            }
        }

        private class ChunkedEncodingReadContent(Stream stream)
            : StreamContent(new ChunkedEncodingReadStream(stream), length: null);

        private class ChunkedEncodingReadStream(Stream innerStream) : DelegatingStream(innerStream)
        {
            private readonly Stream _innerStream = innerStream;
            private readonly StreamReaderHelper _helper = new(innerStream);
            private readonly StringBuilder _sb = new();
            private int _bytesRemainingInChunk = 0;
            private State _state = State.AwaitingChunkHeader;

            private enum State
            {
                AwaitingChunkHeader,
                ReadingChunk,
                ChunkFinished,
                Complete,
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // YOLO
                return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                // This is very crude and doesn't handle edge cases etc, but hopefully it's good enough for tests
                while (true)
                {
                    switch (_state)
                    {
                        case State.AwaitingChunkHeader:
                            // Read the chunk size
                            _sb.Clear();
                            await _helper.ReadUntilNewLine(_sb).ConfigureAwait(false);
                            // annoying, but this is always prepended with an invalid char due to
                            // the way we parse the response. Just hacking around it for simplicity
                            var hexString = _sb.ToString(startIndex: 1, _sb.Length - 1);
                            _bytesRemainingInChunk = int.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
                            _state = State.ReadingChunk;
                            if (_bytesRemainingInChunk == 0)
                            {
                                // we're done
                                _state = State.Complete;
                                // The final chunk has a double newline at the end
                                await _helper.ReadUntilNewLine(_sb);
                                return 0;
                            }

                            break;
                        case State.ReadingChunk:
                            // Read and return the chunk bytes
                            // We might not have enough for the whole chunk, so just read what we can
                            var bytesToRead = Math.Min(count, _bytesRemainingInChunk);
                            var bytesReadFromStream = await _innerStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);

                            // we might not have read the whole chunk, so just return what we have for now
                            _bytesRemainingInChunk -= bytesReadFromStream;
                            if (_bytesRemainingInChunk <= 0)
                            {
                                _state = State.ChunkFinished;
                            }

                            return bytesReadFromStream;
                        case State.ChunkFinished:
                            // Should have a CRLF after the chunk is finished
                            await _helper.GoNextChar();
                            if (!await _helper.IsNewLine())
                            {
                                throw new Exception("Did not receive expected line feed");
                            }

                            // chunk is finished, wait for the next one
                            _state = State.AwaitingChunkHeader;
                            break;
                        case State.Complete:
                            return 0;
                    }
                }
            }
        }
    }
}
