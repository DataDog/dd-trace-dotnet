using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal static class DatadogHttpHeaderWriter
    {
        static readonly byte[] _eol = Encoding.ASCII.GetBytes("\n");

        public static async Task WriteServerResponseStatusAndHeadersAsync(
            this Stream stream,
            string protocol,
            string statusCode,
            string reasonPhrase,
            List<KeyValuePair<string, string>> headers,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var statusLine = $"{protocol} {statusCode} {reasonPhrase}\n";
            var statusLineBytes = Encoding.ASCII.GetBytes(statusLine);
            log("Status line:" + statusLine);
            await stream.WriteAsync(statusLineBytes, 0, statusLineBytes.Length, cancellationToken).ConfigureAwait(false);
            await WriteHeadersAsync(stream, headers, log, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(_eol, 0, _eol.Length, cancellationToken).ConfigureAwait(false);
        }

        private static async Task WriteHeadersAsync(
            Stream stream,
            List<KeyValuePair<string, string>> headers,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            foreach (var header in headers)
            {
                var separator = header.Key == "Server" ? " " : ", ";
                var values = string.Join(separator, header.Value);
                var line = $"{header.Key}: {values}\n";
                log(header.Key + " Header:" + line);
                var payload = Encoding.ASCII.GetBytes(line);
                await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task WriteClientMethodAndHeadersAsync(this Stream stream, TraceRequest request, CancellationToken cancellationToken)
        {
            var firstLine = $"{request.Method} {request.RequestUri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped)} HTTP/{request.Version}\n";
            var payload = Encoding.ASCII.GetBytes(firstLine);

            Debug.WriteLine("-- Client: Writing request - first line");
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine("-- Client: Writing request - headers");
            await WriteHeadersAsync(stream, request.Headers, _ => { }, cancellationToken).ConfigureAwait(false);

            if(request.Traces != null)
            {
                if (!request.Headers.Any())
                {
                    await request.Traces.();
                    // force populating the underlying headers collection
                    request.Headers.ContentLength = request.Content.Headers.ContentLength;
                }

                await WriteHeadersAsync(stream, request.Content.Headers, _ => { }, cancellationToken).ConfigureAwait(false);
            }

            await stream.WriteAsync(_eol, 0, _eol.Length, cancellationToken).ConfigureAwait(false);
        }
    }
}
