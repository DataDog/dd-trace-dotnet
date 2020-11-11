using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            Stream stream = null;
            try
            {
                _logger.Verbose("HttpOS Client: Trying to connect..");
                stream = await _dial.DialAsync(request, cancellationToken).ConfigureAwait(false);

                _logger.Verbose("HttpOS Client: Connected.");
                // request.Properties.Add(UnderlyingStreamProperty, stream);

                _logger.Verbose("HttpOS Client: Writing request");

                // as soon as headers are sent, we should begin reading the response, and send the request body concurrently
                // This is because if the server 404s nothing will ever read the response and it'll hang waiting
                // for someone to read it

                // Cancel this task if server response detected
                var writeTask = Task.Run(
                    async () =>
                    {
                        _logger.Verbose("HttpOS Client: Writing request request.Content.CopyToAsync");
                        var message = FakeHttp.CreatePost(request);
                        var messageBytes = Encoding.ASCII.GetBytes(message);
                        await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken).ConfigureAwait(false);
                        _logger.Verbose("HttpOS Client: stream.FlushAsync");
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        _logger.Verbose("HttpOS Client: Finished writing request");
                    },
                    cancellationToken);

                var response = new TraceResponse() { Request = request };

                // _logger.Verbose("HttpOS Client: Waiting for response");
                // string statusLine = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                // _logger.Verbose("HttpOS Client: Read 1st response line");
                // ParseStatusLine(response, statusLine);
                // _logger.Verbose("HttpOS Client: ParseStatusLine");

                // while (true)
                // {
                //     var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                //     if (line.Length == 0)
                //     {
                //         _logger.Verbose("HttpOS Client: Found empty line, end of response headers");
                //         break;
                //     }
                //     try
                //     {
                //         _logger.Verbose("HttpOS Client: Parsing line:" + line);
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
                var reader = new StreamReader(stream);
                var wholeResponseMessage = reader.ReadToEnd();

                FakeHttp.ReadResponse(response, wholeResponseMessage);

                return response;

                // _logger.Verbose("HttpOS Client: Finished reading response header lines");
                // responseContent.SetContent(
                //     new BodyStream(stream, response.Content.Headers.ContentLength, closeOnReachEnd: true),
                //     response.Content.Headers.ContentLength);
                // return response;
            }
            catch (TimeoutException)
            {
                _logger.Warning("HttpOS Client: connection timed out.");
                stream?.Dispose();
                throw;
            }
            catch (Exception e)
            {
                _logger.Error("HttpOS Client: Exception:" + e.Message);
                stream?.Dispose();
                throw;
            }
        }
    }
}
