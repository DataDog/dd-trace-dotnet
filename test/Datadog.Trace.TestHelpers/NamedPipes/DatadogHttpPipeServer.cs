using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.TestHelpers.NamedPipes.Interfaces;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers.NamedPipes.Server
{
    internal class DatadogHttpPipeServer : IDisposable
    {
        private const int BufferBatchSize = 1;
        private readonly ConcurrentDictionary<int, ListenerState> _activeListeners = new ConcurrentDictionary<int, ListenerState>();
        private readonly string _pipeName;
        private readonly int _maxNumberOfServerInstances;
        private readonly ITestOutputHelper _output;
        private int _requestId = 0;

        public DatadogHttpPipeServer(
            string pipeName,
            int maxNumberOfServerInstances,
            ITestOutputHelper output)
        {
            _pipeName = pipeName;
            _maxNumberOfServerInstances = maxNumberOfServerInstances;
            _output = output;
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        public void Run(CancellationToken cancellationToken)
        {
            // Several listeners for concurrency... I guess
            StartListener();
            StartListener();
            StartListener();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Thread.Sleep(50);
            }
        }

        public void Dispose()
        {
            foreach (var activeListener in _activeListeners)
            {
                var server = activeListener.Value.PipeServer;

                try
                {
                    if (server.IsConnected)
                    {
                        server.Disconnect();
                    }

                    server.Close();
                }
                catch (Exception ex)
                {
                    _output?.WriteLine("Dispose error: {0}", ex);
                }
            }
        }

        public void StopListener(int requestId)
        {
            try
            {
                if (_activeListeners.TryRemove(requestId, out var listener))
                {
                    try
                    {
                        listener.PipeServer.Disconnect();
                        // listener.PipeServer.Close();
                    }
                    catch (Exception ex)
                    {
                        _output?.WriteLine("PipeServer.Disconnect error: {0}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _output?.WriteLine("StopListener error: {0}", ex);
            }
        }

        private void StartListener()
        {
            var requestId = Interlocked.Increment(ref _requestId);

#pragma warning disable CA1416
            var pipeServer = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                _maxNumberOfServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);
#pragma warning restore CA1416

            var listener = new ListenerState() { PipeServer = pipeServer, RequestId = requestId };

            _activeListeners.TryAdd(requestId, listener);

            pipeServer.BeginWaitForConnection(WaitForConnectionCallBack, listener);
        }

        private void OnMessageReceived(MockHttpMessage message)
        {
            MessageReceivedEvent?.Invoke(
                this,
                new MessageReceivedEventArgs { Message = message });
        }

        private void WaitForConnectionCallBack(IAsyncResult result)
        {
            ListenerState listener = null;

            try
            {
                listener = (ListenerState)result.AsyncState;
                listener.PipeServer.EndWaitForConnection(result);
                HandleRequest(listener);
            }
            catch (Exception ex)
            {
                _output?.WriteLine("WaitForConnectionCallBack error: {0}", ex);
            }
            finally
            {
                if (listener != null)
                {
                    StopListener(listener.RequestId);
                }

                StartListener();
            }
        }

        private void HandleRequest(ListenerState listener)
        {
            var pipeServer = listener.PipeServer;

            var batchRead = new byte[BufferBatchSize];
            var headerIndex = 0;
            var headerBuffer = new byte[0x1000];

            var carriageReturn = (byte)'\r';
            var lineFeed = (byte)'\n';

            bool EndOfHeaders()
            {
                if (headerIndex < 3) { return false; }

                if (headerBuffer[headerIndex] != lineFeed) { return false; }

                if (headerBuffer[headerIndex - 1] != carriageReturn) { return false; }

                if (headerBuffer[headerIndex - 2] != lineFeed) { return false; }

                if (headerBuffer[headerIndex - 3] != carriageReturn) { return false; }

                return true;
            }

            var leftoverIndex = 0;
            var leftoverBytes = new byte[BufferBatchSize];

            var endOfHeaders = false;
            do
            {
                pipeServer.Read(batchRead, 0, batchRead.Length);

                foreach (var t in batchRead)
                {
                    if (endOfHeaders)
                    {
                        leftoverBytes[leftoverIndex++] = t;
                    }
                    else
                    {
                        headerBuffer[headerIndex] = t;
                    }

                    if (EndOfHeaders())
                    {
                        endOfHeaders = true;
                        continue;
                    }

                    headerIndex++;
                }
            }
            while (!endOfHeaders);

            var mockMessage = new MockHttpMessage();

            Array.Resize(ref headerBuffer, headerIndex + 1);

            mockMessage.HeaderBytes = headerBuffer;

            var headerString = Encoding.UTF8.GetString(headerBuffer);
            var headerLines = headerString.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None);

            var contentLength = 0;

            foreach (var headerLine in headerLines)
            {
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    continue;
                }

                var semicolonIndex = headerLine.IndexOf(":");
                if (semicolonIndex > -1)
                {
                    var key = headerLine.Substring(0, semicolonIndex);
                    var value = headerLine.Substring(semicolonIndex + 1, headerLine.Length - semicolonIndex - 1);

                    if (key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(value);
                    }

                    mockMessage.Headers.Add(key, value);
                }
            }

            var bodyBytes = new byte[contentLength];

            if (contentLength > 1)
            {
                if (leftoverBytes.All(b => b == (byte)'\0'))
                {
                    // Nothing to copy
                    leftoverIndex = 0;
                }

                if (contentLength < leftoverIndex)
                {
                    Array.Copy(leftoverBytes, bodyBytes, contentLength);
                }
                else
                {
                    var bodyRemainder = contentLength - leftoverIndex;
                    Array.Copy(leftoverBytes, bodyBytes, leftoverIndex);
                    pipeServer.Read(bodyBytes, leftoverIndex, bodyRemainder);
                }
            }

            mockMessage.BodyBytes = bodyBytes;

            OnMessageReceived(mockMessage);

            WriteResponse(pipeServer);

            // Clear the rest if any remains, it's invalid anyways
            // var waste = new byte[1];
            // while (!pipeServer.IsMessageComplete)
            // {
            //     pipeServer.Read(waste, 0, waste.Length);
            // }
        }

        private void WriteResponse(NamedPipeServerStream pipeServer)
        {
            try
            {
                var responseContent = "{}";
                var responseDate = DateTime.Now;
                var responseBuilder = new StringBuilder("HTTP/1.1 200 OK");
                responseBuilder.Append('\r');
                responseBuilder.Append('\n');
                responseBuilder.Append("Content-Type: application/json");
                responseBuilder.Append('\r');
                responseBuilder.Append('\n');
                responseBuilder.Append("Date: ");
                responseBuilder.Append(responseDate.ToUniversalTime().ToString("r"));
                responseBuilder.Append('\r');
                responseBuilder.Append('\n');
                responseBuilder.Append("Content-Length: ");
                responseBuilder.Append(responseContent.Length);
                responseBuilder.Append('\r');
                responseBuilder.Append('\n');
                responseBuilder.Append('\r');
                responseBuilder.Append('\n');
                responseBuilder.Append(responseContent);

                var responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
                pipeServer.Write(responseBytes, 0, responseBytes.Length);
            }
            catch (Exception ex)
            {
                _output?.WriteLine("WriteResponse error: {0}", ex);
            }
        }

        private class ListenerState
        {
            public int RequestId { get; set; }

            public NamedPipeServerStream PipeServer { get; set; }
        }
    }
}
