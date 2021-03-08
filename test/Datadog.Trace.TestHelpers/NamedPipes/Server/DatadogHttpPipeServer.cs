using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using Datadog.Trace.TestHelpers.NamedPipes.Interfaces;
using Datadog.Trace.TestHelpers.NamedPipes.Utilities;

namespace Datadog.Trace.TestHelpers.NamedPipes.Server
{
    internal class DatadogHttpPipeServer
    {
        private const int BufferBatchSize = 0x150;

        private readonly NamedPipeServerStream _pipeServer;

        public DatadogHttpPipeServer(string pipeName, int maxNumberOfServerInstances)
        {
#pragma warning disable CA1416
            _pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);
#pragma warning restore CA1416
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        /// <summary>
        /// This method begins an asynchronous operation to wait for a client to connect.
        /// </summary>
        public void Run()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        _pipeServer.WaitForConnection();
                        HandleRequest();
                    }
                    catch (IOException ex)
                    {
                        Logger.Error(ex);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                        throw;
                    }
                    finally
                    {
                        if (_pipeServer.IsConnected)
                        {
                            _pipeServer.Disconnect();
                        }
                    }
                }
            }
            finally
            {
                _pipeServer.Close();
                _pipeServer.Dispose();
            }
        }

        private void OnMessageReceived(MockHttpMessage message)
        {
            MessageReceivedEvent?.Invoke(
                this,
                new MessageReceivedEventArgs { Message = message });
        }

        private void HandleRequest()
        {
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
                _pipeServer.Read(batchRead, 0, batchRead.Length);

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
                    _pipeServer.Read(bodyBytes, leftoverIndex, bodyRemainder);
                }
            }

            mockMessage.BodyBytes = bodyBytes;

            WriteResponse(_pipeServer);

            // Clear the rest if any remains, it's invalid anyways
            var waste = new byte[0x500];
            while (!_pipeServer.IsMessageComplete)
            {
                _pipeServer.Read(waste, 0, waste.Length);
                var examineWaste = Encoding.UTF8.GetString(waste);
                Logger.Info(examineWaste);
            }

            OnMessageReceived(mockMessage);
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
                Logger.Error(ex);
            }
        }
    }
}
