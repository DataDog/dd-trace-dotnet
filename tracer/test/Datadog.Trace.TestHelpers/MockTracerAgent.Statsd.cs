// <copyright file="MockTracerAgent.Statsd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// A mock agent that can be used to test the tracer.
/// </summary>
public abstract partial class MockTracerAgent
{
    private abstract class StatsdAgent(CancellationTokenSource cts)
    {
        public ConcurrentQueue<string> StatsdRequests { get; } = new();

        public ConcurrentQueue<Exception> StatsdExceptions { get; } = new();

        protected CancellationTokenSource CancellationTokenSource { get; } = cts;
    }

    private class StatsdUdpAgent : StatsdAgent, IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly Task _statsdTask;

        public StatsdUdpAgent(int retries, int? requestedStatsDPort, CancellationTokenSource cts)
            : base(cts)
        {
            if (requestedStatsDPort != null)
            {
                // This port is explicit, allow failure if not available
                StatsdPort = requestedStatsDPort.Value;
                _udpClient = new UdpClient(requestedStatsDPort.Value);
            }
            else
            {
                const int basePort = 11555;

                var retriesLeft = retries;

                while (true)
                {
                    try
                    {
                        _udpClient = new UdpClient(basePort + retriesLeft);
                    }
                    catch (Exception) when (retriesLeft > 0)
                    {
                        retriesLeft--;
                        continue;
                    }

                    StatsdPort = basePort + retriesLeft;
                    break;
                }
            }

            _statsdTask = Task.Factory.StartNew(HandleStatsdRequests, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Gets the UDP port for statsd
        /// </summary>
        public int StatsdPort { get; }

        public void Dispose()
        {
            _udpClient?.Close();
        }

        private void HandleStatsdRequests()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 0);

            while (!CancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var buffer = _udpClient.Receive(ref endPoint);
                    var stats = Encoding.UTF8.GetString(buffer);
                    StatsdRequests.Enqueue(stats);
                }
                catch (Exception) when (CancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    StatsdExceptions.Enqueue(ex);
                }
            }
        }
    }

    private class StatsdNamedPipeAgent : StatsdAgent, IDisposable
    {
        private readonly PipeServer _statsPipeServer;

        public StatsdNamedPipeAgent(string metricsPipeName, CancellationTokenSource cts)
            : base(cts)
        {
            if (File.Exists(metricsPipeName))
            {
                File.Delete(metricsPipeName);
            }

            StatsWindowsPipeName = metricsPipeName;

            _statsPipeServer = new PipeServer(
                metricsPipeName,
                PipeDirection.In, // we don't send responses to stats requests
                CancellationTokenSource,
                (stream, ct) => HandleNamedPipeStats(stream, ct),
                ex => StatsdExceptions.Enqueue(ex),
                x => Output?.WriteLine(x));

            _statsPipeServer.Start();
        }

        public string StatsWindowsPipeName { get; }

        public ITestOutputHelper Output { get; set; }

        public void Dispose()
        {
            _statsPipeServer?.Dispose();
        }

        private async Task HandleNamedPipeStats(NamedPipeServerStream namedPipeServerStream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(namedPipeServerStream);

            while (await reader.ReadLineAsync() is { } request)
            {
                StatsdRequests.Enqueue(request);
            }
        }

        internal class PipeServer : IDisposable
        {
            private const int ConcurrentInstanceCount = 5;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly string _pipeName;
            private readonly PipeDirection _pipeDirection;
            private readonly Func<NamedPipeServerStream, CancellationToken, Task> _handleReadFunc;
            private readonly Action<Exception> _handleExceptionFunc;
            private readonly ConcurrentBag<Task> _tasks = new();
            private readonly Action<string> _log;
            private int _instanceCount = 0;

            public PipeServer(
                string pipeName,
                PipeDirection pipeDirection,
                CancellationTokenSource tokenSource,
                Func<NamedPipeServerStream, CancellationToken, Task> handleReadFunc,
                Action<Exception> handleExceptionFunc,
                Action<string> log)
            {
                _cancellationTokenSource = tokenSource;
                _pipeDirection = pipeDirection;
                _pipeName = pipeName;
                _handleReadFunc = handleReadFunc;
                _handleExceptionFunc = handleExceptionFunc;
                _log = log;
            }

            public void Start()
            {
                for (var i = 0; i < ConcurrentInstanceCount; i++)
                {
                    _log("Starting PipeServer " + _pipeName);
                    using var mutex = new ManualResetEventSlim();
                    var startPipe = StartNamedPipeServer(mutex);
                    _tasks.Add(startPipe);
                    mutex.Wait(5_000);
                }
            }

            public void Dispose()
            {
                _log("Waiting for PipeServer Disposal " + _pipeName);
                Task.WaitAll(_tasks.ToArray(), TimeSpan.FromSeconds(10));
            }

            private async Task StartNamedPipeServer(ManualResetEventSlim mutex)
            {
                var instance = $" ({_pipeName}:{Interlocked.Increment(ref _instanceCount)})";
                try
                {
                    _log("Starting NamedPipeServerStream instance " + instance);
                    using var statsServerStream = new NamedPipeServerStream(
                        _pipeName,
                        _pipeDirection, // we don't send responses to stats requests
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    _log("Starting wait for connection " + instance);
                    var connectTask = statsServerStream.WaitForConnectionAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                    mutex.Set();

                    _log("Awaiting connection " + instance);
                    await connectTask;

                    _log("Connection accepted, starting new server" + instance);

                    // start a new Named pipe server to handle additional connections
                    // Yes, this is madness, but apparently the way it's supposed to be done
                    using var m = new ManualResetEventSlim();
                    _tasks.Add(Task.Run(() => StartNamedPipeServer(m)));
                    // Wait for the next instance to start listening before we handle this one
                    m.Wait(5_000);

                    _log("Executing read for " + instance);

                    await _handleReadFunc(statsServerStream, _cancellationTokenSource.Token);
                }
                catch (Exception) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    _log("Execution canceled " + instance);
                }
                catch (IOException ex) when (ex.Message.Contains("The pipe is being closed"))
                {
                    // Likely interrupted by a dispose
                    // Swallow the exception and let the test finish
                    _log("Pipe closed " + instance);
                }
                catch (Exception ex)
                {
                    _handleExceptionFunc(ex);

                    // unexpected exception, so start another listener
                    _log("Unexpected exception " + instance + " " + ex.ToString());
                    using var m = new ManualResetEventSlim();
                    _tasks.Add(Task.Run(() => StartNamedPipeServer(m)));
                    m.Wait(5_000);
                }
            }
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    private class StatsdUdsAgent : StatsdAgent, IDisposable
    {
        private readonly UnixDomainSocketEndPoint _statsEndpoint;
        private readonly Socket _udsStatsSocket;
        private readonly Task _statsdTask;

        public StatsdUdsAgent(string metricSocket, CancellationTokenSource cts)
            : base(cts)
        {
            if (File.Exists(metricSocket))
            {
                File.Delete(metricSocket);
            }

            StatsUdsPath = metricSocket;
            _statsEndpoint = new UnixDomainSocketEndPoint(metricSocket);

            _udsStatsSocket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);

            _udsStatsSocket.Bind(_statsEndpoint);
            // NOTE: Connectionless protocols don't use Listen()
            _statsdTask = Task.Factory.StartNew(HandleUdsStats, TaskCreationOptions.LongRunning);
        }

        public string StatsUdsPath { get; }

        public void Dispose()
        {
            // In versions before net6, dispose doesn't shutdown this socket type for some reason
            IgnoreException(() => _udsStatsSocket.Shutdown(SocketShutdown.Both));
            IgnoreException(() => _udsStatsSocket.Close());
            IgnoreException(() => _udsStatsSocket.Dispose());
            IgnoreException(() => File.Delete(StatsUdsPath));

            void IgnoreException(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    StatsdExceptions.Enqueue(ex);
                }
            }
        }

        private void HandleUdsStats()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var bytesReceived = new byte[0x1000];
                    // Connectionless protocol doesn't need Accept, Receive will block until we get something
                    var byteCount = _udsStatsSocket.Receive(bytesReceived);
                    var stats = Encoding.UTF8.GetString(bytesReceived, 0, byteCount);
                    StatsdRequests.Enqueue(stats);
                }
                catch (Exception) when (CancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    var message = ex.Message.ToLowerInvariant();
                    if (message.Contains("interrupted"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    if (message.Contains("broken") || message.Contains("forcibly closed") || message.Contains("invalid argument"))
                    {
                        // The application was likely shut down
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    StatsdExceptions.Enqueue(ex);
                }
            }
        }
    }
#endif
}
