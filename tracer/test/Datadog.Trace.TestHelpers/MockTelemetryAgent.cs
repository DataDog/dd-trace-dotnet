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
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.TestHelpers
{
    public class MockTelemetryAgent<T> : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;

        // Needs to be kept in sync with JsonTelemetryTransportBase.SerializerSettings, but with the additional converter
        private readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            NullValueHandling = JsonTelemetryTransportBase.SerializerSettings.NullValueHandling,
            ContractResolver = JsonTelemetryTransportBase.SerializerSettings.ContractResolver,
            Converters = new List<JsonConverter> { new PayloadConverter() },
        });

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

                    _listenerThread = new Thread(HandleHttpRequests);
                    _listenerThread.Start();

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

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        /// <summary>
        /// Gets or sets a value indicating whether to skip serialization of traces.
        /// </summary>
        public bool ShouldDeserializeTraces { get; set; } = true;

        /// <summary>
        /// Gets the TCP port that this Agent is listening on.
        /// Can be different from <see cref="MockTelemetryAgent"/>'s <c>initialPort</c>
        /// parameter if listening on that port fails.
        /// </summary>
        public int Port { get; }

        public ConcurrentStack<T> Telemetry { get; } = new();

        public IImmutableList<NameValueCollection> RequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        /// <summary>
        /// Wait for the telemetry condition to be satisfied.
        /// Note that the first telemetry that satisfies the condition is returned
        /// To retrieve all telemetry received, use <see cref="Telemetry"/>
        /// </summary>
        /// <param name="hasExpectedValues">A predicate for the current telemetry</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <param name="sleepTime">The time between checks</param>
        /// <returns>The telemetry that satisfied <paramref name="hasExpectedValues"/></returns>
        public T WaitForLatestTelemetry(
            Func<T, bool> hasExpectedValues,
            int timeoutInMilliseconds = 5000,
            int sleepTime = 200)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            T latest = default;
            while (DateTime.UtcNow < deadline)
            {
                if (Telemetry.TryPeek(out latest) && hasExpectedValues(latest))
                {
                    break;
                }

                Thread.Sleep(sleepTime);
            }

            return latest;
        }

        public void Dispose()
        {
            _listener?.Close();
        }

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            RequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(context));
        }

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    OnRequestReceived(ctx);

                    T telemetry;
                    using (var sr = new StreamReader(ctx.Request.InputStream))
                    using (var jsonTextReader = new JsonTextReader(sr))
                    {
                        telemetry = _serializer.Deserialize<T>(jsonTextReader);
                    }

                    Telemetry.Push(telemetry);

                    lock (this)
                    {
                        RequestHeaders = RequestHeaders.Add(new NameValueCollection(ctx.Request.Headers));
                    }

                    // NOTE: HttpStreamRequest doesn't support Transfer-Encoding: Chunked
                    // (Setting content-length avoids that)

                    ctx.Response.StatusCode = 200;
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

        internal class PayloadConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(IPayload);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                // use the default serialization - it works fine
                serializer.Serialize(writer, value);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                object payload = serializer.Deserialize<AppStartedPayload>(reader);

                payload ??= serializer.Deserialize<AppDependenciesLoadedPayload>(reader);
                payload ??= serializer.Deserialize<AppIntegrationsChangedPayload>(reader);
                payload ??= serializer.Deserialize<GenerateMetricsPayload>(reader);

                if (payload is null)
                {
                    throw new Exception("Unknown IPayload type");
                }

                return payload;
            }
        }
    }
}
