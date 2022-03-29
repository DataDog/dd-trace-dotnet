// <copyright file="MockDatadogAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Text;
using System.Threading;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class MockDatadogAgent : IDisposable
    {
        private readonly Thread _listenerThread;
        private HttpListener _listener;
        private ITestOutputHelper _output;

        public MockDatadogAgent(ITestOutputHelper output, int retries = 5)
        {
            _output = output;

            Initialize(retries);

            _listenerThread = new Thread(HandleHttpRequests);
            _listenerThread.Start();
        }

        public int Port { get; private set; }
        public int NbReceivedCalls { get; private set; }

        public void Dispose()
        {
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
            while (_listener.IsListening)
            {
                string message = null;
                try
                {
                    var ctx = _listener.GetContext();
                    NbReceivedCalls++;

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
                    message = $"NbReceivedCalls = {NbReceivedCalls} | HttpListenerException({x.ErrorCode}, {x.NativeErrorCode}): {x.Message}";
                }
                catch (ObjectDisposedException x)
                {
                    // the response has been already disposed.
                    message = $"NbReceivedCalls = {NbReceivedCalls} | ObjectDisposedException: {x.Message}";
                }
                catch (InvalidOperationException x)
                {
                    // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                    // for now ignore, and we'll see if this introduces downstream issues
                    message = $"NbReceivedCalls = {NbReceivedCalls} | InvalidOperationException: {x.Message}";
                }
                catch (Exception x) when (!_listener.IsListening)
                {
                    // we don't care about any exception when listener is stopped
                    message = $"{x.GetType()}: {x.Message}";
                }

                // show only in error cases
                if ((message != null) && (NbReceivedCalls < 2))
                {
                    Console.WriteLine(message);
                }
            }
        }
    }
}
