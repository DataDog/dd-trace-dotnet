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
        /// Typical response from the agent is ~148 bytes.
        /// Allow enough room for failure messages and future expanding.
        /// </summary>
        public const int MaxResponseBufferSize = 5120;

        /// <summary>
        /// Typical headers sent to the agent are small.
        /// Allow enough room for future expansion of headers.
        /// </summary>
        public const int MaxRequestHeadersBufferSize = 2560;

        private static readonly Vendors.Serilog.ILogger Logger = DatadogLogging.For<DatadogHttpClient>();

        public async Task<HttpResponse> SendAsync(HttpRequest request, Stream requestStream, Stream responseStream)
        {
            await SendRequestAsync(request, requestStream).ConfigureAwait(false);
            return await ReadResponseAsync(responseStream).ConfigureAwait(false);
        }

        private async Task SendRequestAsync(HttpRequest request, Stream requestStream)
        {
            // Headers are always ASCII per the HTTP spec
            using (var writer = new StreamWriter(requestStream, Encoding.ASCII, MaxRequestHeadersBufferSize, leaveOpen: true))
            {
                await DatadogHttpHeaderHelper.WriteLeadingHeaders(request, writer).ConfigureAwait(false);

                foreach (var header in request.Headers)
                {
                    await DatadogHttpHeaderHelper.WriteHeader(writer, header).ConfigureAwait(false);
                }

                await DatadogHttpHeaderHelper.WriteEndOfHeaders(writer).ConfigureAwait(false);
            }

            await request.Content.CopyToAsync(requestStream).ConfigureAwait(false);
            Logger.Debug("Datadog HTTP: Flushing stream.");
            await requestStream.FlushAsync().ConfigureAwait(false);
        }

        private async Task<HttpResponse> ReadResponseAsync(Stream responseStream)
        {
            var headers = new HttpHeaders();
            int statusCode = 0;
            string reasonPhrase = null;
            char currentChar;

            // hack: buffer the entire response so we can seek
            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            memoryStream.Position = 0;
            int streamPosition = 0;

            // The stream we read back from the agent will usually be a few hundred bytes
            using (var reader = new StreamReader(memoryStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, (int)memoryStream.Length, leaveOpen: true))
            {
                // https://tools.ietf.org/html/rfc2616#section-4.2
                // HTTP/1.1 200 OK
                // HTTP/1.1 XXX MESSAGE

                const int statusCodeStart = 9;
                const int statusCodeEnd = 12;
                const int startOfReasonPhrase = 13;

                // TODO: Get these from a StringBuilderCache
                var keyBuilder = new StringBuilder();
                var valueBuilder = new StringBuilder();

                var chArray = new byte[1];
                void GoNextChar()
                {
                    streamPosition++;
                    chArray[0] = (byte)reader.Read();
                    currentChar = Encoding.ASCII.GetChars(chArray)[0];
                }

                bool IsNewLine()
                {
                    if (currentChar.Equals(DatadogHttpHeaderHelper.CarriageReturn))
                    {
                        // end of headers
                        if (DatadogHttpHeaderHelper.CrLfLength > 1)
                        {
                            // Skip the newline indicator
                            GoNextChar();
                        }

                        return true;
                    }

                    return false;
                }

                while (streamPosition < statusCodeStart)
                {
                    GoNextChar();
                }

                while (streamPosition < statusCodeEnd)
                {
                    GoNextChar();
                    keyBuilder.Append(currentChar);
                }

                while (streamPosition < statusCodeStart)
                {
                    GoNextChar();
                }

                while (streamPosition < startOfReasonPhrase)
                {
                    GoNextChar();
                }

                do
                {
                    GoNextChar();
                    if (IsNewLine())
                    {
                        break;
                    }

                    valueBuilder.Append(currentChar);
                }
                while (true);

                var potentialStatusCode = keyBuilder.ToString();
                if (!int.TryParse(potentialStatusCode, out statusCode))
                {
                    throw new DatadogHttpRequestException("Invalid response, can't parse status code. Line was:" + potentialStatusCode);
                }

                reasonPhrase = valueBuilder.ToString();

                keyBuilder.Clear();
                valueBuilder.Clear();

                // read headers
                do
                {
                    GoNextChar();

                    if (IsNewLine())
                    {
                        // Empty line, content starts next
                        break;
                    }

                    do
                    {
                        if (currentChar.Equals(':'))
                        {
                            // Value portion starts
                            break;
                        }

                        keyBuilder.Append(currentChar);
                        GoNextChar();
                    }
                    while (true);

                    do
                    {
                        GoNextChar();

                        if (IsNewLine())
                        {
                            // Next header pair starts
                            break;
                        }

                        valueBuilder.Append(currentChar);
                    }
                    while (true);

                    var name = keyBuilder.ToString().Trim();
                    var value = valueBuilder.ToString().Trim();

                    headers.Add(name, value);

                    keyBuilder.Clear();
                    valueBuilder.Clear();
                }
                while (true);
            }

            memoryStream.Position = streamPosition;
            long bytesLeft = memoryStream.Length - memoryStream.Position;
            var length = long.TryParse(headers.GetValue("Content-Length"), out var headerValue) ? headerValue : (long?)null;

            if (length == null)
            {
                length = bytesLeft;
            }
            else if (length != bytesLeft)
            {
                throw new DatadogHttpRequestException("Content length from http headers does not match content's actual length.");
            }

            return new HttpResponse(statusCode, reasonPhrase, headers, new StreamContent(memoryStream, length));
        }
    }
}
