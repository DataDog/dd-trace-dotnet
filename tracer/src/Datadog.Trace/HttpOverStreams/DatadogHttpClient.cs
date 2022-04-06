// <copyright file="DatadogHttpClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.HttpOverStreams
{
    internal class DatadogHttpClient
    {
        /// <summary>
        /// Typical headers sent to the agent are small.
        /// Allow enough room for future expansion of headers.
        /// </summary>
        private const int MaxRequestHeadersBufferSize = 4096;
        private const string ContentLengthHeaderKey = "Content-Length";

        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DatadogHttpClient>();

        private readonly HttpHeaderHelperBase _headerHelper;

        private DatadogHttpClient(HttpHeaderHelperBase headerHelper)
        {
            _headerHelper = headerHelper;
        }

        public static DatadogHttpClient CreateTraceAgentClient()
            => new DatadogHttpClient(new TraceAgentHttpHeaderHelper());

        public async Task<HttpResponse> SendAsync(HttpRequest request, Stream requestStream, Stream responseStream)
        {
            await SendRequestAsync(request, requestStream).ConfigureAwait(false);
            return await ReadResponseAsync(responseStream).ConfigureAwait(false);
        }

        private async Task SendRequestAsync(HttpRequest request, Stream requestStream)
        {
            // Headers are always ASCII per the HTTP spec
            using (var writer = new StreamWriter(requestStream, Encoding.ASCII, bufferSize: MaxRequestHeadersBufferSize, leaveOpen: true))
            {
                await _headerHelper.WriteLeadingHeaders(request, writer).ConfigureAwait(false);

                foreach (var header in request.Headers)
                {
                    await _headerHelper.WriteHeader(writer, header).ConfigureAwait(false);
                }

                // Empty line to signify end of headers
                await writer.WriteAsync(DatadogHttpValues.CrLf).ConfigureAwait(false);

                // Remove (admittedly really small) sync over async occurrence
                // by forcing a flush so that System.IO.TextWriter.Dispose() does not block
                await writer.FlushAsync().ConfigureAwait(false);
            }

            await request.Content.CopyToAsync(requestStream).ConfigureAwait(false);
            Logger.Debug("Datadog HTTP: Flushing stream.");
            await requestStream.FlushAsync().ConfigureAwait(false);
        }

        private async Task<HttpResponse> ReadResponseAsync(Stream responseStream)
        {
            var headers = new HttpHeaders();
            char currentChar = char.MinValue;
            int streamPosition = 0;

            // https://tools.ietf.org/html/rfc2616#section-4.2
            // HTTP/1.1 200 OK
            // HTTP/1.1 XXX MESSAGE

            const int statusCodeStart = 9;
            const int statusCodeEnd = 12;
            const int startOfReasonPhrase = 13;
            const int bufferSize = 10;

            // TODO: Get this from StringBuilderCache after we determine safe maximum capacity
            var stringBuilder = new StringBuilder();

            var chArray = new byte[bufferSize];

            async Task GoNextChar()
            {
                var bytesRead = await responseStream.ReadAsync(chArray, offset: 0, count: 1).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    ThrowHelper.ThrowInvalidOperationException($"Unexpected end of stream at position {streamPosition}");
                }

                currentChar = Encoding.ASCII.GetChars(chArray)[0];
                streamPosition++;
            }

            async Task SkipUntil(int requiredStreamPosition)
            {
                var requiredBytes = requiredStreamPosition - streamPosition;
                if (requiredBytes < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException("StreamPosition already exceeds requiredStreamPosition", nameof(requiredStreamPosition));
                }

                var bytesRemaining = requiredBytes;
                var lastBytesRead = 0;
                while (bytesRemaining > 0)
                {
                    var bytesToRead = Math.Min(bytesRemaining, bufferSize);
                    lastBytesRead = await responseStream.ReadAsync(chArray, offset: 0, count: bytesToRead).ConfigureAwait(false);
                    if (lastBytesRead == 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException($"Unexpected end of stream at position {streamPosition}");
                    }

                    bytesRemaining -= lastBytesRead;
                }

                currentChar = Encoding.ASCII.GetChars(chArray)[lastBytesRead - 1];
                streamPosition += requiredBytes;
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

                    await ReadUntil(builder, DatadogHttpValues.CarriageReturn).ConfigureAwait(false);
                }
                while (true);
            }

            async Task<bool> IsNewLine()
            {
                if (currentChar.Equals(DatadogHttpValues.CarriageReturn))
                {
                    // end of headers
                    // Next character should be a LineFeed, regardless of Linux/Windows
                    // Skip the newline indicator
                    await GoNextChar().ConfigureAwait(false);

                    if (!currentChar.Equals(DatadogHttpValues.LineFeed))
                    {
                        ThrowHelper.ThrowException($"Unexpected character {currentChar} in headers: CR must be followed by LF");
                    }

                    return true;
                }

                return false;
            }

            // Skip to status code
            await SkipUntil(statusCodeStart).ConfigureAwait(false);

            // Read status code
            while (streamPosition < statusCodeEnd)
            {
                await GoNextChar().ConfigureAwait(false);
                stringBuilder.Append(currentChar);
            }

            var potentialStatusCode = stringBuilder.ToString();
            stringBuilder.Clear();

            if (!int.TryParse(potentialStatusCode, out var statusCode))
            {
                DatadogHttpRequestException.Throw("Invalid response, can't parse status code. Line was:" + potentialStatusCode);
            }

            // Skip to reason
            await SkipUntil(startOfReasonPhrase).ConfigureAwait(false);

            // Read reason
            await GoNextChar().ConfigureAwait(false);
            await ReadUntilNewLine(stringBuilder).ConfigureAwait(false);

            var reasonPhrase = stringBuilder.ToString();
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

            return new HttpResponse(statusCode, reasonPhrase, headers, new StreamContent(responseStream, length));
        }
    }
}
