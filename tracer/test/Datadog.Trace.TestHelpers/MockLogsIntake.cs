// <copyright file="MockLogsIntake.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.TestHelpers
{
    public class MockLogsIntake<T> : IDisposable
    {
        private static readonly JsonSerializer JsonSerializer = new();
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MockLogsIntake(int? initialPort = null, int retries = 5)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var port = initialPort ?? TcpPortProvider.GetOpenPort();

            // try up to 5 consecutive ports before giving up
            while (true)
            {
                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Prefixes.Add($"http://127.0.0.1:{port}/v1/input/");
                listener.Prefixes.Add($"http://localhost:{port}/v1/input/");
                listener.Prefixes.Add($"http://127.0.0.1:{port}/api/v2/logs/");
                listener.Prefixes.Add($"http://localhost:{port}/api/v2/logs/");

                try
                {
                    listener.Start();

                    // successfully listening
                    Port = port;
                    _listener = listener;

                    _listenerThread = new Thread(HandleHttpRequests);
                    _listenerThread.IsBackground = true;
                    _listenerThread.Start();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port = TcpPortProvider.GetOpenPort();
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        public event EventHandler<EventArgs<IList<T>>> RequestDeserialized;

        /// <summary>
        /// Gets the TCP port that this intake is listening on.
        /// Can be different from <see cref="MockLogsIntake"/>'s <c>initialPort</c>
        /// parameter if listening on that port fails.
        /// </summary>
        public int Port { get; }

        public IImmutableList<T> Logs { get; private set; } = ImmutableList<T>.Empty;

        public IImmutableList<NameValueCollection> RequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to skip deserialization of traces.
        /// </summary>
        public bool ShouldDeserializeLogs { get; set; } = true;

        public void Dispose()
        {
            _listener?.Stop();
            _cancellationTokenSource.Cancel();
        }

        internal static List<T> DeserializeFromStream(Stream stream)
        {
            using var sr = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(sr);
            return JsonSerializer.Deserialize<List<T>>(jsonTextReader);
        }

        private void AssertHeader(
            NameValueCollection headers,
            string headerKey,
            Func<string, bool> assertion)
        {
            var header = headers.Get(headerKey);

            if (string.IsNullOrEmpty(header))
            {
                throw new Exception($"Every submission to the intake should have a {headerKey} header.");
            }

            if (!assertion(header))
            {
                throw new Exception($"Failed assertion for {headerKey} on {header}");
            }
        }

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    if (ShouldDeserializeLogs)
                    {
                        var logs = DeserializeFromStream(ctx.Request.InputStream);
                        RequestDeserialized?.Invoke(this, new EventArgs<IList<T>>(logs));

                        lock (this)
                        {
                            // we only need to lock when replacing the logs collection,
                            // not when reading it because it is immutable
                            Logs = Logs.AddRange(logs);
                            RequestHeaders = RequestHeaders.Add(new NameValueCollection(ctx.Request.Headers));
                        }
                    }

                    // NOTE: HttpStreamRequest doesn't support Transfer-Encoding: Chunked
                    // (Setting content-length avoids that)

                    ctx.Response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes("{}");
                    ctx.Response.ContentLength64 = buffer.LongLength;
                    ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    ctx.Response.Close();
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

    public class MockLogsIntake : MockLogsIntake<MockLogsIntake.Log>
    {
        public class Log
        {
            [JsonProperty("@t")]
            public DateTimeOffset Timestamp { get; set; }

            [JsonProperty("@m")]
            public string Message { get; set; }

            [JsonProperty("@i")]
            public string EventId { get; set; }

            [JsonProperty("@x")]
            public Exception Exception { get; set; }

            // Not required, defaults to Information if not provided
            [JsonProperty("@l")]
            public DirectSubmissionLogLevel LogLevel { get; set; } = DirectSubmissionLogLevel.Information;

            [JsonProperty("ddsource")]
            public string Source { get; set; }

            [JsonProperty("host")]
            public string Host { get; set; }

            [JsonProperty("ddtags")]
            public string Tags { get; set; }

            [JsonProperty("dd_env")]
            public string Env { get; set; }

            [JsonProperty("dd_version")]
            public string Version { get; set; }

            [JsonProperty("dd_service")]
            public string Service { get; set; }

            [JsonProperty("dd_trace_id")]
            public string TraceId { get; set; }

            [JsonProperty("dd_span_id")]
            public string SpanId { get; set; }

            [JsonExtensionData]
            public Dictionary<string, object> OtherProperties { get; } = new();

            [JsonProperty("service")]
            private string Service1
            {
                set => Service = value;
            }

            [JsonProperty("dd.service")]
            private string Service2
            {
                set => Service = value;
            }

            [JsonProperty("dd.trace_id")]
            private string TraceId1
            {
                set => TraceId = value;
            }

            [JsonProperty("dd.span_id")]
            private string SpanId1
            {
                set => SpanId = value;
            }

            [JsonProperty("dd.env")]
            private string Env2
            {
                set => Env = value;
            }

            [JsonProperty("dd.version")]
            private string Version2
            {
                set => Version = value;
            }

            // Using tuple return instead of out, as can't use out parameters in FluentAssertion expressions
            public (bool Exists, string Value) TryGetProperty(string key) =>
                !OtherProperties.TryGetValue(key, out var obj)
                    ? (false, null)
                    : (true, obj.ToString());
        }
    }
}
