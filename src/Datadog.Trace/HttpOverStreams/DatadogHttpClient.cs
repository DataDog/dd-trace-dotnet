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
        private const int BufferSize = 10240;
        private static readonly Vendors.Serilog.ILogger Logger = DatadogLogging.For<DatadogHttpClient>();

        public Task<HttpResponse> SendAsync(HttpRequest request, Stream requestStream, Stream responseStream)
        {
            Task.Run(() => SendRequest(request, requestStream));
            return ReadResponse(responseStream);
        }

        private async Task SendRequest(HttpRequest request, Stream requestStream)
        {
            // TODO: Determine if it's always ASCII
            using (var writer = new StreamWriter(requestStream, Encoding.ASCII, BufferSize, leaveOpen: true))
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

        private async Task<HttpResponse> ReadResponse(Stream responseStream)
        {
            var headers = new HttpHeaders();
            int statusCode = 0;
            string responseMessage = null;

            // hack: buffer the entire response so we can seek
            var memoryStream = new MemoryStream();
            responseStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            int streamPosition = 0;

            using (var reader = new StreamReader(memoryStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, BufferSize, leaveOpen: true))
            {
                // HTTP/1.1 200 OK
                // HTTP/1.1 XXX MESSAGE
                string line = reader.ReadLine();
                streamPosition += reader.CurrentEncoding.GetByteCount(line) + DatadogHttpHeaderHelper.CrLfLength;

                const int statusCodeStart = 9;
                const int statusCodeLength = 3;
                const int startOfMessage = 13;

                if (!int.TryParse(line.Substring(statusCodeStart, statusCodeLength), out statusCode))
                {
                    throw new DatadogHttpRequestException("Invalid response, can't parse status code. Line was:" + line);
                }

                responseMessage = line.Substring(startOfMessage);

                // read headers
                while (true)
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                    streamPosition += reader.CurrentEncoding.GetByteCount(line) + DatadogHttpHeaderHelper.CrLfLength;

                    if (line == string.Empty)
                    {
                        // end of headers
                        break;
                    }

                    var headerParts = line.Split(':');

                    if (headerParts.Length != 2)
                    {
                        Logger.Warning("Malformed header: {0}", line);
                        continue;
                    }

                    var name = headerParts[0].Trim();
                    var value = headerParts[1].Trim();
                    headers.Add(name, value);
                }
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

            return new HttpResponse(statusCode, responseMessage, headers, new StreamContent(memoryStream, length));
        }
    }
}
