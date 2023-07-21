// <copyright file="MockHttpParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Profiler.IntegrationTests.Helpers.HttpOverStreams;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class MockHttpParser
    {
        private const string ContentLengthHeaderKey = "Content-Length";

        public static async Task<MockHttpRequest> ReadRequest(Stream stream)
        {
            var headers = new HttpHeaders();
            char currentChar = char.MinValue;
            int streamPosition = 0;

            // https://tools.ietf.org/html/rfc2616#section-4.2
            const int bufferSize = 10;

            var stringBuilder = new StringBuilder();

            var chArray = new byte[bufferSize];

            async Task GoNextChar()
            {
                var bytesRead = await stream.ReadAsync(chArray, offset: 0, count: 1).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException($"Unexpected end of stream at position {streamPosition}");
                }

                currentChar = Encoding.ASCII.GetChars(chArray)[0];
                streamPosition++;
            }

            async Task ReadUntil(StringBuilder builder, char stopChar)
            {
                while (!currentChar.Equals(stopChar))
                {
                    builder.Append(currentChar);
                    await GoNextChar().ConfigureAwait(false);
                }
            }

            async Task ReadUntilNewLine(StringBuilder builder)
            {
                do
                {
                    if (await IsNewLine().ConfigureAwait(false))
                    {
                        break;
                    }

                    await ReadUntil(builder, MockDatadogAgent.DatadogHttpValues.CarriageReturn).ConfigureAwait(false);
                }
                while (true);
            }

            async Task<bool> IsNewLine()
            {
                if (currentChar.Equals(MockDatadogAgent.DatadogHttpValues.CarriageReturn))
                {
                    // end of headers
                    // Next character should be a LineFeed, regardless of Linux/Windows
                    // Skip the newline indicator
                    await GoNextChar().ConfigureAwait(false);

                    if (!currentChar.Equals(MockDatadogAgent.DatadogHttpValues.LineFeed))
                    {
                        throw new Exception($"Unexpected character {currentChar} in headers: CR must be followed by LF");
                    }

                    return true;
                }

                return false;
            }

            stringBuilder.Clear();

            // Read POST
            await ReadUntil(stringBuilder, stopChar: ' ').ConfigureAwait(false);

            var method = stringBuilder.ToString();
            stringBuilder.Clear();

            // Read /path?request
            await GoNextChar().ConfigureAwait(false);
            await ReadUntil(stringBuilder, stopChar: ' ').ConfigureAwait(false);

            var pathAndQuery = stringBuilder.ToString().Trim();
            stringBuilder.Clear();

            // Skip to end of line
            await ReadUntilNewLine(stringBuilder).ConfigureAwait(false);
            stringBuilder.Clear();

            // Read headers
            do
            {
                await GoNextChar().ConfigureAwait(false);

                // Check for end of headers
                if (await IsNewLine().ConfigureAwait(false))
                {
                    // Empty line, content starts next
                    break;
                }

                // Read key
                await ReadUntil(stringBuilder, stopChar: ':').ConfigureAwait(false);

                var name = stringBuilder.ToString().Trim();
                stringBuilder.Clear();

                // skip separator
                await GoNextChar().ConfigureAwait(false);

                // Read value
                await ReadUntilNewLine(stringBuilder).ConfigureAwait(false);

                var value = stringBuilder.ToString().Trim();
                stringBuilder.Clear();

                headers.Add(name, value);
            }
            while (true);

            var length = long.TryParse(headers.GetValue(ContentLengthHeaderKey), out var headerValue) ? headerValue : (long?)null;

            return new MockHttpRequest()
            {
                Headers = headers,
                Method = method,
                PathAndQuery = pathAndQuery,
                ContentLength = length,
                Body = new StreamContent(stream, length)
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
    }
}
