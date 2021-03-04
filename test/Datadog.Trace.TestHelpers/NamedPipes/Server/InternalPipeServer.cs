using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.TestHelpers.NamedPipes.Interfaces;
using Datadog.Trace.TestHelpers.NamedPipes.Utilities;

namespace Datadog.Trace.TestHelpers.NamedPipes.Server
{
    internal class InternalPipeServer : ICommunicationServer
    {
        private const int BufferBatchSize = 0x150;
        private static int _seed = 0;

        private readonly NamedPipeServerStream _pipeServer;
        private readonly object _lockingObject = new object();

        private bool _isStopping;
        private int _lockFlag = 0;

        public InternalPipeServer(string pipeName, int maxNumberOfServerInstances)
        {
#pragma warning disable CA1416
            _pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);
#pragma warning restore CA1416
            Interlocked.Increment(ref _seed);
            Id = _seed.ToString();
        }

        public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;

        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEvent;

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        public string Id { get; }

        public string ServerId
        {
            get { return Id; }
        }

        /// <summary>
        /// This method begins an asynchronous operation to wait for a client to connect.
        /// </summary>
        public void Start()
        {
            try
            {
                _pipeServer.BeginWaitForConnection(WaitForConnectionCallBack, null);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// This method disconnects, closes and disposes the server
        /// </summary>
        public void Stop()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            _isStopping = true;

            try
            {
                if (_pipeServer.IsConnected)
                {
                    _pipeServer.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
            finally
            {
                _pipeServer.Close();
                _pipeServer.Dispose();
            }
        }

        private void BeginRead()
        {
            try
            {
                ReadRequest(_pipeServer);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
        }

        private void WaitForConnectionCallBack(IAsyncResult result)
        {
            AggregatePipeServer.Timeline.Push($"Internal Client connection callback: {Id}");

            if (Interlocked.CompareExchange(ref _lockFlag, 1, 0) == 0)
            {
                var unlocked = Monitor.TryEnter(_lockingObject);
                if (unlocked)
                {
                    if (!_isStopping)
                    {
                        // Call EndWaitForConnection to complete the connection operation
                        _pipeServer.EndWaitForConnection(result);

                        OnConnected();

                        AggregatePipeServer.Timeline.Push($"Internal Client begin read: {Id}");
                        BeginRead();
                    }

                    Interlocked.Decrement(ref _lockFlag);
                }
            }
        }

        private void OnMessageReceived(MockHttpMessage message)
        {
            MessageReceivedEvent?.Invoke(
                this,
                new MessageReceivedEventArgs { Message = message });
        }

        private void OnConnected()
        {
            ClientConnectedEvent?.Invoke(this, new ClientConnectedEventArgs { ClientId = Id });
            AggregatePipeServer.Timeline.Push($"Internal Client connected: {Id}");
        }

        private void OnDisconnected()
        {
            ClientDisconnectedEvent?.Invoke(this, new ClientDisconnectedEventArgs { ClientId = Id });
            AggregatePipeServer.Timeline.Push($"Internal Client disconnected: {Id}");
        }

        private void ReadRequest(NamedPipeServerStream pipeServer)
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

            // Let's write back
            // var responseString = @$"";

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

            // Clear the rest if any remains, it's invalid anyways
            var waste = new byte[0x500];
            while (!pipeServer.IsMessageComplete)
            {
                pipeServer.Read(waste, 0, waste.Length);
                var examineWaste = Encoding.UTF8.GetString(waste);
                Logger.Info(examineWaste);
            }

            OnMessageReceived(mockMessage);

            OnDisconnected();
            Stop();
        }
    }
}
