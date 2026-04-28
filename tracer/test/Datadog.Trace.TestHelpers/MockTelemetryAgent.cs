// <copyright file="MockTelemetryAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    internal class MockTelemetryAgent : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _listenerTask;

        public MockTelemetryAgent(int port = 8524, int retries = 5)
        {
            // try up to 5 consecutive ports before giving up
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

                    // successfully listening
                    Port = port;
                    _listener = listener;

                    _listenerTask = HandleHttpRequests();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port = TcpPortProvider.GetOpenPort();
                    retries--;
                }
                catch
                {
                    // always close listener if exception is thrown,
                    // whether it was caught or not
                    listener.Close();
                    throw;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        /// <summary>
        /// Gets the TCP port that this Agent is listening on.
        /// Can be different from <see cref="MockTelemetryAgent"/>'s <c>initialPort</c>
        /// parameter if listening on that port fails.
        /// </summary>
        public int Port { get; }

        public ConcurrentStack<object> Telemetry { get; } = new();

        public IImmutableList<NameValueCollection> RequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public void Dispose()
        {
            _listener?.Close();
        }

        internal static object DeserializeResponse(Stream inputStream, string apiVersion, string requestType, bool compressed)
        {
            return null;
        }

        protected virtual void HandleHttpRequest(HttpListenerContext ctx)
        {
            var apiVersion = ctx.Request.Headers[TelemetryConstants.ApiVersionHeader];
            var requestType = ctx.Request.Headers[TelemetryConstants.RequestTypeHeader];
            var compressed = ctx.Request.Headers["Content-Encoding"].Equals("gzip", StringComparison.OrdinalIgnoreCase);

            var telemetry = DeserializeResponse(ctx.Request.InputStream, apiVersion, requestType, compressed);
            Telemetry.Push(telemetry);

            lock (this)
            {
                RequestHeaders = RequestHeaders.Add(new NameValueCollection(ctx.Request.Headers));
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        }

        private async Task HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    HandleHttpRequest(ctx);
                }
                catch (HttpListenerException)
                {
                    // listener was stopped,
                    // ignore to let the loop end and the method return
                }
                catch (ObjectDisposedException)
                {
                    // the response has been already disposed.
                }
                catch (InvalidOperationException)
                {
                    // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                    // for now ignore, and we'll see if this introduces downstream issues
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    // we don't care about any exception when listener is stopped
                }
            }
        }
    }
}
