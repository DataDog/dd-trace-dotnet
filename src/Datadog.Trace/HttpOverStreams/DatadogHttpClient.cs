using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Logging;

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

        public async Task<HttpResponse> SendAsync(HttpRequest request, Stream requestStream, Stream responseStream)
        {
            await SendRequestAsync(request, requestStream).ConfigureAwait(false);
            return await ReadResponseAsync(responseStream);
        }

        private async Task SendRequestAsync(HttpRequest request, Stream requestStream)
        {
            // Headers are always ASCII per the HTTP spec
            using (var writer = new StreamWriter(requestStream, Encoding.ASCII, bufferSize: MaxRequestHeadersBufferSize, leaveOpen: true))
            {
                await DatadogHttpHeaderHelper.WriteLeadingHeaders(request, writer).ConfigureAwait(false);

                foreach (var header in request.Headers)
                {
                    await DatadogHttpHeaderHelper.WriteHeader(writer, header).ConfigureAwait(false);
                }

                await DatadogHttpHeaderHelper.WriteEndOfHeaders(writer).ConfigureAwait(false);
            }

            await request.Content.CopyToAsync(requestStream, null).ConfigureAwait(false);
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

            // TODO: Get this from StringBuilderCache after we determine safe maximum capacity
            var stringBuilder = new StringBuilder();

            var chArray = new byte[1];

            async Task GoNextChar()
            {
                await responseStream.ReadAsync(chArray, offset: 0, count: 1);
                currentChar = Encoding.ASCII.GetChars(chArray)[0];
                streamPosition++;
            }

            async Task<bool> IsNewLine()
            {
                if (currentChar.Equals(DatadogHttpValues.CarriageReturn))
                {
                    // end of headers
                    if (DatadogHttpValues.CrLfLength > 1)
                    {
                        // Skip the newline indicator
                        await GoNextChar();
                    }

                    return true;
                }

                return false;
            }

            // Skip to status code
            while (streamPosition < statusCodeStart)
            {
                await GoNextChar();
            }

            // Read status code
            while (streamPosition < statusCodeEnd)
            {
                await GoNextChar();
                stringBuilder.Append(currentChar);
            }

            var potentialStatusCode = stringBuilder.ToString();
            stringBuilder.Clear();

            if (!int.TryParse(potentialStatusCode, out var statusCode))
            {
                throw new DatadogHttpRequestException("Invalid response, can't parse status code. Line was:" + potentialStatusCode);
            }

            // Skip to reason
            while (streamPosition < startOfReasonPhrase)
            {
                await GoNextChar();
            }

            // Read reason
            do
            {
                await GoNextChar();
                if (await IsNewLine())
                {
                    break;
                }

                stringBuilder.Append(currentChar);
            }
            while (true);

            var reasonPhrase = stringBuilder.ToString();
            stringBuilder.Clear();

            // Read headers
            do
            {
                await GoNextChar();

                // Check for end of headers
                if (await IsNewLine())
                {
                    // Empty line, content starts next
                    break;
                }

                // Read key
                do
                {
                    if (currentChar.Equals(':'))
                    {
                        // Value portion starts
                        break;
                    }

                    stringBuilder.Append(currentChar);
                    await GoNextChar();
                }
                while (true);

                var name = stringBuilder.ToString().Trim();
                stringBuilder.Clear();

                // Read value
                do
                {
                    await GoNextChar();

                    if (await IsNewLine())
                    {
                        // Next header pair starts
                        break;
                    }

                    stringBuilder.Append(currentChar);
                }
                while (true);

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
