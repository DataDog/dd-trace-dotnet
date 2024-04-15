// <copyright file="MockHttpParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Util;

namespace Datadog.Trace.TestHelpers
{
    internal class MockHttpParser
    {
        private const string ContentLengthHeaderKey = "Content-Length";

        public static async Task<MockHttpRequest> ReadRequest(Stream stream)
        {
            var headers = new MockHttpRequest.MockHeaders();
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

            var length = headers.TryGetValue(ContentLengthHeaderKey, out var contentLength)
                             ? long.Parse(contentLength)
                             : (long?)null;

            var body = headers.TryGetValue("Transfer-Encoding", out var header) && header is "chunked"
                           ? new ChunkedEncodingReadStream(stream)
                           : stream;

            return new MockHttpRequest()
            {
                Headers = headers,
                Method = method,
                PathAndQuery = pathAndQuery,
                ContentLength = length,
                Body = body
            };
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
    }
}
