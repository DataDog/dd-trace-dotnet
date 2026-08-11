// <copyright file="MockFeatureFlagsIntake.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Stands in for the agentless (CDN-backed) flag configuration endpoint: serves the JSON:API
    /// Universal Flag Configuration envelope, honours <c>If-None-Match</c>, and records every
    /// request so tests can assert what the tracer sent.
    /// </summary>
    internal class MockFeatureFlagsIntake : IDisposable
    {
        // The wire format is camelCase, and the parser validates the envelope's own keys
        // case-sensitively, so the serializer has to match the real endpoint.
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly HttpListener _listener;
        private readonly Task _listenerTask;
        private string _body;

        public MockFeatureFlagsIntake(ServerConfiguration configuration, int retries = 5)
        {
            _body = BuildEnvelope(configuration);

            var port = TcpPortProvider.GetOpenPort();

            while (true)
            {
                // A listener that failed to start cannot be reused, so build a new one per attempt.
                var listener = new HttpListener();

                // A single catch-all prefix: tests point the tracer at both the origin (so it appends
                // the canonical path) and at explicit paths.
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Prefixes.Add($"http://localhost:{port}/");

                try
                {
                    listener.Start();

                    Port = port;
                    _listener = listener;
                    _listenerTask = HandleHttpRequests();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    port = TcpPortProvider.GetOpenPort();
                    retries--;
                }

                listener.Close();
            }
        }

        /// <summary>
        /// Gets the TCP port this intake is listening on.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the origin to use as <c>DD_FEATURE_FLAGS_CONFIGURATION_SOURCE_AGENTLESS_BASE_URL</c>.
        /// It carries no path, so the tracer appends the canonical one.
        /// </summary>
        public string Origin => $"http://localhost:{Port}";

        public IImmutableList<ReceivedRequest> Requests { get; private set; } = ImmutableList<ReceivedRequest>.Empty;

        /// <summary>
        /// Gets or sets the ETag served with the configuration. A request presenting it gets a 304.
        /// </summary>
        public string ETag { get; set; } = "\"ufc-1\"";

        /// <summary>
        /// Gets or sets a value indicating whether the response body is gzipped, as the real endpoint's is.
        /// </summary>
        public bool UseGzip { get; set; } = true;

        /// <summary>
        /// Gets or sets the status code to answer with. Anything other than 200 is served without a body.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        public void SetConfiguration(ServerConfiguration configuration, string etag)
        {
            // Set the body first: a request landing in between must not be answered with the new
            // ETag and the old body.
            Volatile.Write(ref _body, BuildEnvelope(configuration));
            ETag = etag;
        }

        /// <summary>
        /// Waits until at least <paramref name="count"/> requests have been received.
        /// </summary>
        public async Task<IImmutableList<ReceivedRequest>> WaitForRequests(int count, int timeoutMs = 30_000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                var requests = Requests;
                if (requests.Count >= count)
                {
                    return requests;
                }

                await Task.Delay(100);
            }

            return Requests;
        }

        public void Dispose()
        {
            _listener?.Stop();
        }

        private static string BuildEnvelope(ServerConfiguration configuration)
        {
            var attributes = JsonConvert.SerializeObject(configuration, SerializerSettings);
            return $"{{\"data\":{{\"id\":\"ufc\",\"type\":\"universal-flag-configuration\",\"attributes\":{attributes}}}}}";
        }

        private static byte[] Gzip(byte[] payload)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(payload, 0, payload.Length);
            }

            return output.ToArray();
        }

        private async Task HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    var received = new ReceivedRequest(ctx.Request.Url!.PathAndQuery, new NameValueCollection(ctx.Request.Headers));

                    lock (this)
                    {
                        Requests = Requests.Add(received);
                    }

                    Respond(ctx, received);
                }
                catch (HttpListenerException)
                {
                    // The listener was stopped: let the loop end.
                }
                catch (ObjectDisposedException)
                {
                    // The response was already disposed.
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    // Anything thrown while shutting down is uninteresting.
                }
            }
        }

        private void Respond(HttpListenerContext ctx, ReceivedRequest received)
        {
            var etag = ETag;

            if (StatusCode != 200)
            {
                ctx.Response.StatusCode = StatusCode;
                ctx.Response.ContentLength64 = 0;
                ctx.Response.Close();
                return;
            }

            if (received.IfNoneMatch == etag)
            {
                // A 304 carries no body, and HttpListener rejects a content length on one.
                ctx.Response.StatusCode = 304;
                ctx.Response.Headers["ETag"] = etag;
                ctx.Response.Close();
                return;
            }

            var buffer = Encoding.UTF8.GetBytes(Volatile.Read(ref _body));
            if (UseGzip)
            {
                buffer = Gzip(buffer);
                ctx.Response.Headers["Content-Encoding"] = "gzip";
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["ETag"] = etag;

            // HttpStreamRequest doesn't support Transfer-Encoding: chunked, which setting the
            // content length avoids.
            ctx.Response.ContentLength64 = buffer.LongLength;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        }

        internal class ReceivedRequest
        {
            public ReceivedRequest(string pathAndQuery, NameValueCollection headers)
            {
                PathAndQuery = pathAndQuery;
                Headers = headers;
            }

            public string PathAndQuery { get; }

            public NameValueCollection Headers { get; }

            public string? IfNoneMatch => Headers["If-None-Match"];
        }
    }
}
