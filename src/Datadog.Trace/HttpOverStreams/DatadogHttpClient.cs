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
        public const int MaxRequestHeadersBufferSize = 2560;

        private static readonly Vendors.Serilog.ILogger Logger = DatadogLogging.For<DatadogHttpClient>();

        public async Task<HttpResponse> SendAsync(HttpRequest request, Stream requestStream, Stream responseStream)
        {
            await SendRequestAsync(request, requestStream).ConfigureAwait(false);
            return ReadResponse(responseStream);
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

        private HttpResponse ReadResponse(Stream responseStream)
        {
            var headers = new HttpHeaders();
            char currentChar;

            int streamPosition = 0;

            // https://tools.ietf.org/html/rfc2616#section-4.2
            // HTTP/1.1 200 OK
            // HTTP/1.1 XXX MESSAGE

            const int statusCodeStart = 9;
            const int statusCodeEnd = 12;
            const int startOfReasonPhrase = 13;

            // TODO: Get this from a StringBuilderCache
            var stringBuilder = new StringBuilder();

            var chArray = new byte[1];
            void GoNextChar()
            {
                streamPosition++;
                chArray[0] = (byte)responseStream.ReadByte();
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
                stringBuilder.Append(currentChar);
            }

            var potentialStatusCode = stringBuilder.ToString();
            stringBuilder.Clear();

            if (!int.TryParse(potentialStatusCode, out var statusCode))
            {
                throw new DatadogHttpRequestException("Invalid response, can't parse status code. Line was:" + potentialStatusCode);
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

                stringBuilder.Append(currentChar);
            }
            while (true);

            var reasonPhrase = stringBuilder.ToString();
            stringBuilder.Clear();

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

                    stringBuilder.Append(currentChar);
                    GoNextChar();
                }
                while (true);

                var name = stringBuilder.ToString().Trim();
                stringBuilder.Clear();

                do
                {
                    GoNextChar();

                    if (IsNewLine())
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

            var length = long.TryParse(headers.GetValue("Content-Length"), out var headerValue) ? headerValue : (long?)null;

            return new HttpResponse(statusCode, reasonPhrase, headers, new StreamContent(responseStream, length));
        }
    }
}
