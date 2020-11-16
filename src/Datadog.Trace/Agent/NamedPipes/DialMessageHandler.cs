using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class DialMessageHandler
    {
        public const string UnderlyingStreamProperty = "DIAL_UNDERLYING_STREAM";
        private readonly IDial _dial;
        private readonly Version _httpVersion = new Version(1, 0);
        private readonly ILogger _logger = Log.ForContext<DialMessageHandler>();

        public DialMessageHandler(IDial dial)
        {
            _dial = dial ?? throw new ArgumentNullException(nameof(dial));
        }

        public async Task<TraceResponse> SendAsync(TraceRequest request, CancellationToken cancellationToken)
        {
            Stream namedPipeClientStream = null;
            try
            {
                _logger.Verbose("Pipe Client: Trying to connect..");
                namedPipeClientStream = await _dial.DialAsync(request, cancellationToken).ConfigureAwait(false);

                _logger.Verbose("Pipe Client: Connected.");
                // request.Properties.Add(UnderlyingStreamProperty, stream);

                _logger.Verbose("Pipe Client: Writing request");

                // as soon as headers are sent, we should begin reading the response, and send the request body concurrently
                // This is because if the server 404s nothing will ever read the response and it'll hang waiting
                // for someone to read it

                // Cancel this task if server response detected
                // var writeTask = Task.Run(
                //     async () =>
                //     {
                //         _logger.Verbose("Pipe Client: Writing request request.Content.CopyToAsync");
                //         var message = FakeHttp.CreatePost(request);
                //         var messageBytes = Encoding.ASCII.GetBytes(message);
                //         await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken).ConfigureAwait(false);
                //         _logger.Verbose("Pipe Client: stream.FlushAsync");
                //         await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                //         _logger.Verbose("Pipe Client: Finished writing request");
                //     },
                //     cancellationToken);

                _logger.Verbose("Pipe Client: Writing request");
                var messageEndBytes = new byte[] { 0x0d, 0x0a, 0x30, 0x0d, 0x0a, 0x0d, 0x0a };

                var headerBytes = FakeHttp.SerializeHeaders(request);
                // var message = await FakeHttp.CreatePost(request);

                var id = Guid.NewGuid();
                using (var fs = File.Create($"C:\\_INSPECTION\\FakeHttp\\payload-{id}.txt"))
                {
                    fs.Write(headerBytes, 0, headerBytes.Length);
                    await CachedSerializer.Instance.SerializeAsync(fs, request.Traces, new FormatterResolverWrapper(SpanFormatterResolver.Instance));
                    // fs.Write(messageEndBytes, 0, messageEndBytes.Length);
                    await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                namedPipeClientStream.Write(headerBytes, 0, headerBytes.Length);
                await CachedSerializer.Instance.SerializeAsync(namedPipeClientStream, request.Traces, new FormatterResolverWrapper(SpanFormatterResolver.Instance));
                // namedPipeClientStream.Write(messageEndBytes, 0, messageEndBytes.Length);
                await namedPipeClientStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                _logger.Verbose("Pipe Client: Finished writing request");

                var response = new TraceResponse() { Request = request };

                // _logger.Verbose("Pipe Client: Waiting for response");
                // string statusLine = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                // _logger.Verbose("Pipe Client: Read 1st response line");
                // ParseStatusLine(response, statusLine);
                // _logger.Verbose("Pipe Client: ParseStatusLine");

                // while (true)
                // {
                //     var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                //     if (line.Length == 0)
                //     {
                //         _logger.Verbose("Pipe Client: Found empty line, end of response headers");
                //         break;
                //     }
                //     try
                //     {
                //         _logger.Verbose("Pipe Client: Parsing line:" + line);
                //         (var name, var value) = HttpParser.ParseHeaderNameValues(line);
                //         if (!response.Headers.TryAddWithoutValidation(name, value))
                //         {
                //             response.Content.Headers.TryAddWithoutValidation(name, value);
                //         }
                //     }
                //     catch (FormatException ex)
                //     {
                //         throw new HttpRequestException("Error parsing header", ex);
                //     }
                // }

                // convert stream to string
                var reader = new StreamReader(namedPipeClientStream);
                var wholeResponseMessage = reader.ReadToEnd();

                FakeHttp.ReadResponse(response, wholeResponseMessage);

                return response;

                // _logger.Verbose("Pipe Client: Finished reading response header lines");
                // responseContent.SetContent(
                //     new BodyStream(stream, response.Content.Headers.ContentLength, closeOnReachEnd: true),
                //     response.Content.Headers.ContentLength);
                // return response;
            }
            catch (TimeoutException)
            {
                _logger.Warning("Pipe Client: connection timed out.");
                namedPipeClientStream?.Dispose();
                throw;
            }
            catch (Exception e)
            {
                _logger.Error("Pipe Client: Exception:" + e.Message);
                namedPipeClientStream?.Dispose();
                // throw;
                var response = new TraceResponse();
                FakeHttp.ReadResponse(response, null);
                return response;
            }
        }
    }
}
