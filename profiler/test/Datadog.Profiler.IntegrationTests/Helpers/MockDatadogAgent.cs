// <copyright file="MockDatadogAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public abstract class MockDatadogAgent : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ManualResetEventSlim _readinessNotifier = new();
        private AgentEtwProxy _etwProxy = null;

        public event EventHandler<EventArgs<HttpListenerContext>> ProfilerRequestReceived;
        public event EventHandler<EventArgs<HttpListenerContext>> TracerRequestReceived;
        public event EventHandler<EventArgs<HttpListenerContext>> TelemetryMetricsRequestReceived;
        public event EventHandler<EventArgs<int>> ProfilerRegistered;
        public event EventHandler<EventArgs<int>> EventsSent;
        public event EventHandler<EventArgs<int>> ProfilerUnregistered;

        public int NbCallsOnProfilingEndpoint { get; private set; }

        public bool IsReady => _readinessNotifier.Wait(TimeSpan.FromSeconds(30)); // wait for Agent being ready

        public bool ProfilerHasRegistered { get => (_etwProxy != null) ? _etwProxy.ProfilerHasRegistered : false; }

        public bool ProfilerHasUnregistered { get => (_etwProxy != null) ? _etwProxy.ProfilerHasUnregistered : false; }

        public bool EventsHaveBeenSent { get => (_etwProxy != null) ? _etwProxy.EventsHaveBeenSent : false; }

        protected ITestOutputHelper Output { get; set; }

        public static HttpAgent CreateHttpAgent(ITestOutputHelper output, int retries = 5) => new HttpAgent(output, retries);

        public static NamedPipeAgent CreateNamedPipeAgent(ITestOutputHelper output) => new NamedPipeAgent(output);

        public void StartEtwProxy(ITestOutputHelper output, string namedPipeEndpoint, string eventsFilename = null)
        {
            // simulate the Agent as an ETW proxy to replay events (if any)
            // 1. create a named pipe server with the given endpoint to receive registration/unregistration commands from the profiler
            //    --> keep track of the register/unregister to be able to validate the protocol in a test
            // 2. read the events from the given file and send them to the profiler
            //    --> keep track of any error
            //    --> if no file is provided, don't send any event but accept registration/unregistration commands
            // NOTE: this method must be called before calling Run() on the TestApplicationRunner
            _etwProxy = new AgentEtwProxy(output, namedPipeEndpoint, eventsFilename);
            _etwProxy.ProfilerRegistered += (sender, e) => ProfilerRegistered?.Invoke(this, e);
            _etwProxy.EventsSent += (sender, e) => EventsSent?.Invoke(this, e);
            _etwProxy.ProfilerUnregistered += (sender, e) => ProfilerUnregistered?.Invoke(this, e);

            _etwProxy.StartServer();
        }

        public virtual void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _readinessNotifier.Dispose();
        }

        private void OnProfilesRequestReceived(HttpListenerContext ctx)
        {
            ProfilerRequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(ctx));
        }

        private void OnTracesRequestReceived(HttpListenerContext ctx)
        {
            TracerRequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(ctx));
        }

        private void OnTelemetryMetricsReceived(HttpListenerContext ctx)
        {
            TelemetryMetricsRequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(ctx));
        }

        public class HttpAgent : MockDatadogAgent
        {
            private const string ProfilesEndpoint = "/profiling/v1/input";
            private const string TracesEndpoint = "/v0.4/traces";
            private const string TelemetryMetricsEndpoint = "/telemetry/proxy/api/v2/apmtelemetry";

            private readonly Thread _listenerThread;
            private HttpListener _listener;

            public HttpAgent(ITestOutputHelper output, int retries)
            {
                Output = output;

                Initialize(retries);

                _listenerThread = new Thread(HandleHttpRequests);
                _listenerThread.Start();
            }

            public int Port { get; private set; }

            public override void Dispose()
            {
                base.Dispose();
                _listener?.Stop();
            }

            private void Initialize(int retries)
            {
                var port = TcpPortProvider.GetOpenPort();
                while (true)
                {
                    // seems like we can't reuse a listener if it fails to start,
                    // so create a new listener each time we retry
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Prefixes.Add($"http://localhost:{port}/");

                    try
                    {
                        listener.Start();
                        Port = port;
                        _listener = listener;

                        // success
                        return;
                    }
                    catch (HttpListenerException) when (retries > 0)
                    {
                        // only catch the exception if there are retries left
                        port = TcpPortProvider.GetOpenPort();
                        retries--;
                    }

                    listener.Close();
                }
            }

            private void HandleHttpRequests()
            {
                _readinessNotifier.Set();

                while (_listener.IsListening)
                {
                    string message = null;
                    try
                    {
                        var ctx = _listener.GetContext();
                        if (ctx.Request.RawUrl == ProfilesEndpoint)
                        {
                            OnProfilesRequestReceived(ctx);
                            NbCallsOnProfilingEndpoint++;
                        }

                        if (ctx.Request.RawUrl == TracesEndpoint)
                        {
                            OnTracesRequestReceived(ctx);
                        }

                        if (ctx.Request.RawUrl == TelemetryMetricsEndpoint)
                        {
                            OnTelemetryMetricsReceived(ctx);
                        }

                        // NOTE: HttpStreamRequest doesn't support Transfer-Encoding: Chunked
                        // (Setting content-length avoids that)

                        ctx.Response.ContentType = "application/json";
                        var buffer = Encoding.UTF8.GetBytes("{}");
                        ctx.Response.ContentLength64 = buffer.LongLength;
                        ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        ctx.Response.Close();
                    }
                    catch (HttpListenerException x)
                    {
                        // listener was stopped,
                        // ignore to let the loop end and the method return
                        message = $"NbReceivedCalls = {NbCallsOnProfilingEndpoint} | HttpListenerException({x.ErrorCode}, {x.NativeErrorCode}): {x.Message}";
                    }
                    catch (ObjectDisposedException x)
                    {
                        // the response has been already disposed.
                        message = $"NbReceivedCalls = {NbCallsOnProfilingEndpoint} | ObjectDisposedException: {x.Message}";
                    }
                    catch (InvalidOperationException x)
                    {
                        // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                        // for now ignore, and we'll see if this introduces downstream issues
                        message = $"NbReceivedCalls = {NbCallsOnProfilingEndpoint} | InvalidOperationException: {x.Message}";
                    }
                    catch (Exception x) when (!_listener.IsListening)
                    {
                        // we don't care about any exception when listener is stopped
                        message = $"{x.GetType()}: {x.Message}";
                    }

                    // show only in error cases
                    if ((message != null) && (NbCallsOnProfilingEndpoint < 2))
                    {
                        // NOTE: we can't use ITestOutputHelper here because it will throw an exception about missing current test
                        // Output.WriteLine(message);
                    }
                }
            }
        }

        public class NamedPipeAgent : MockDatadogAgent
        {
            private readonly PipeServer _namedPipeServer;
            private readonly Task _profilesListenerTask;
            private readonly byte[] _responseBytes;

            private int _nbTime = 0;

            public NamedPipeAgent(ITestOutputHelper output)
            {
                Output = output;
                var sb = new StringBuilder();
                sb
                   .Append("HTTP/1.1 ")
                   .Append("200")
                   .Append(DatadogHttpValues.CrLf)
                   .Append("Date: ")
                   .Append(DateTime.UtcNow.ToString("ddd, dd MMM yyyy H:mm::ss K"))
                   .Append(DatadogHttpValues.CrLf)
                   .Append("Connection: close")
                   .Append(DatadogHttpValues.CrLf)
                   .Append("Server: dd-mock-agent");

                var responseBody = Encoding.UTF8.GetBytes("{}");
                var contentLength64 = responseBody.LongLength;

                sb
                   .Append(DatadogHttpValues.CrLf)
                   .Append("Content-Type: application/json")
                   .Append(DatadogHttpValues.CrLf)
                   .Append("Content-Length: ")
                   .Append(contentLength64)
                   .Append(DatadogHttpValues.CrLf)
                   .Append(DatadogHttpValues.CrLf)
                   .Append(Encoding.ASCII.GetString(responseBody));

                _responseBytes = Encoding.UTF8.GetBytes(sb.ToString());

                ProfilesPipeName = $"profile-{Guid.NewGuid()}";
                _namedPipeServer = new PipeServer(
                    ProfilesPipeName,
                    PipeDirection.InOut,
                    _cancellationTokenSource,
                    (ss, t) => HandleNamedPipeProfiles(ss, t),
                    x => Output.WriteLine(x),
                    _readinessNotifier);

                _profilesListenerTask = _namedPipeServer.Start();
            }

            public string ProfilesPipeName { get; }

            public override void Dispose()
            {
                base.Dispose();
                _namedPipeServer?.Dispose();
            }

            // For now just empty the pipe stream
            private async Task HandleNamedPipeProfiles(NamedPipeServerStream ss, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _nbTime);

                while (ss.IsConnected)
                {
                    _ = MockHttpParser.ReadRequest(ss);
                    await ss.WriteAsync(_responseBytes, cancellationToken);
                    NbCallsOnProfilingEndpoint++;
                }
            }
        }

        internal static class DatadogHttpValues
        {
            public const char CarriageReturn = '\r';
            public const char LineFeed = '\n';
            public const string CrLf = "\r\n";
        }

        internal class PipeServer : IDisposable
        {
            private const int ConcurrentInstanceCount = 5;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly string _pipeName;
            private readonly PipeDirection _pipeDirection;
            private readonly Func<NamedPipeServerStream, CancellationToken, Task> _handleReadFunc;
            private readonly ConcurrentBag<Task> _tasks = new();
            private readonly Action<string> _log;
            private readonly ManualResetEventSlim _readinessNotifier;
            private int _instanceCount = 0;

            public PipeServer(
                string pipeName,
                PipeDirection pipeDirection,
                CancellationTokenSource tokenSource,
                Func<NamedPipeServerStream, CancellationToken, Task> handleReadFunc,
                Action<string> log,
                ManualResetEventSlim readinessNotifier)
            {
                _cancellationTokenSource = tokenSource;
                _pipeDirection = pipeDirection;
                _pipeName = pipeName;
                _handleReadFunc = handleReadFunc;
                _log = log;
                _readinessNotifier = readinessNotifier;
            }

            public Task Start()
            {
                for (var i = 0; i < ConcurrentInstanceCount; i++)
                {
                    _log("Starting PipeServer " + _pipeName);
                    using var stopEvent = new ManualResetEventSlim();
                    var startPipe = StartNamedPipeServer(stopEvent);
                    _tasks.Add(startPipe);
                    stopEvent.Wait(5_000);
                }

                return Task.CompletedTask;
            }

            public void Dispose()
            {
                _log("Waiting for PipeServer Disposal " + _pipeName);
                Task.WaitAll(_tasks.ToArray(), TimeSpan.FromSeconds(10));
            }

            private async Task StartNamedPipeServer(ManualResetEventSlim stopEvent)
            {
                var instance = $" ({_pipeName}:{Interlocked.Increment(ref _instanceCount)})";
                try
                {
                    _log("Starting NamedPipeServerStream instance " + instance);
                    using var serverStream = new NamedPipeServerStream(
                        _pipeName,
                        _pipeDirection,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    _readinessNotifier.Set();

                    _log("Starting wait for connection " + instance);
                    var connectTask = serverStream.WaitForConnectionAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                    stopEvent.Set();

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

                    await _handleReadFunc(serverStream, _cancellationTokenSource.Token);
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
                    // unexpected exception, so start another listener
                    _log("Unexpected exception " + instance + " " + ex.ToString());
                    using var m = new ManualResetEventSlim();
                    _tasks.Add(Task.Run(() => StartNamedPipeServer(m)));
                    m.Wait(5_000);
                }
            }
        }
    }
}
