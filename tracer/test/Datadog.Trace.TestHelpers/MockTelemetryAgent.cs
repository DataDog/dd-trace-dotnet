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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    internal class MockTelemetryAgent : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;

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

        public bool OptionalHeaders { get; set; }

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

        public ConcurrentStack<TelemetryData> Telemetry { get; } = new();

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
        public TelemetryData WaitForLatestTelemetry(
            Func<TelemetryData, bool> hasExpectedValues,
            int timeoutInMilliseconds = 10_000,
            int sleepTime = 500)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            while (DateTime.UtcNow < deadline)
            {
                var current = Telemetry;
                foreach (var telemetry in current)
                {
                    if (hasExpectedValues(telemetry))
                    {
                        return telemetry;
                    }
                }

                Thread.Sleep(sleepTime);
            }

            return null;
        }

        public void Dispose()
        {
            _listener?.Close();
        }

        internal static TelemetryData DeserializeResponse(Stream inputStream, string apiVersion, string requestType)
        {
            return apiVersion switch
            {
                TelemetryConstants.ApiVersionV2 => DeserializeV2(inputStream, requestType),
                _ => throw new Exception($"Unknown telemetry api version: {apiVersion}"),
            };

            static TelemetryData DeserializeV2(Stream inputStream, string requestType)
            {
                if (!TelemetryConverter.V2Serializers.TryGetValue(requestType, out var serializer))
                {
                    throw new Exception($"Unknown V2 telemetry request type {requestType}");
                }

                using var sr = new StreamReader(inputStream);
                var text = sr.ReadToEnd();
                var tr = new StringReader(text);
                using var jsonTextReader = new JsonTextReader(tr);
                var telemetry = serializer.Deserialize<TelemetryData>(jsonTextReader);

                return telemetry;
            }
        }

        protected virtual void HandleHttpRequest(HttpListenerContext ctx)
        {
            var apiVersion = ctx.Request.Headers[TelemetryConstants.ApiVersionHeader];
            var requestType = ctx.Request.Headers[TelemetryConstants.RequestTypeHeader];

            var inputStream = ctx.Request.InputStream;

            if (OptionalHeaders && (apiVersion == null || requestType == null))
            {
                using var sr = new StreamReader(inputStream);
                var text = sr.ReadToEnd();

                var json = JObject.Parse(text);
                apiVersion = json["api_version"].Value<string>();
                requestType = json["request_type"].Value<string>();
                inputStream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            }

            var telemetry = DeserializeResponse(inputStream, apiVersion, requestType);
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

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
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

        internal class TelemetryConverter
        {
            public static readonly Dictionary<string, JsonSerializer> V2Serializers;

            static TelemetryConverter()
            {
                V2Serializers = new()
                {
                    { TelemetryRequestTypes.MessageBatch, CreateSerializer<MessageBatchPayload>() },
                    { TelemetryRequestTypes.AppStarted, CreateSerializer<AppStartedPayload>() },
                    { TelemetryRequestTypes.AppDependenciesLoaded, CreateSerializer<AppDependenciesLoadedPayload>() },
                    { TelemetryRequestTypes.AppIntegrationsChanged, CreateSerializer<AppIntegrationsChangedPayload>() },
                    { TelemetryRequestTypes.AppClientConfigurationChanged, CreateSerializer<AppClientConfigurationChangedPayload>() },
                    { TelemetryRequestTypes.AppProductChanged, CreateSerializer<AppProductChangePayload>() },
                    { TelemetryRequestTypes.GenerateMetrics, CreateSerializer<GenerateMetricsPayload>() },
                    { TelemetryRequestTypes.Distributions, CreateSerializer<DistributionsPayload>() },
                    { TelemetryRequestTypes.RedactedErrorLogs, CreateLogsSerializer() },
                    { TelemetryRequestTypes.AppExtendedHeartbeat, CreateSerializer<AppExtendedHeartbeatPayload>() },
                    { TelemetryRequestTypes.AppClosing, CreateNullPayloadSerializer() },
                    { TelemetryRequestTypes.AppHeartbeat, CreateNullPayloadSerializer() },
                };
            }

            // Needs to be kept in sync with JsonTelemetryTransport.SerializerSettings, but with the additional converter
            private static JsonSerializer CreateSerializer<TPayload>() =>
                JsonSerializer.Create(new JsonSerializerSettings
                {
                    NullValueHandling = JsonTelemetryTransport.SerializerSettings.NullValueHandling,
                    ContractResolver = JsonTelemetryTransport.SerializerSettings.ContractResolver,
                    Converters = new List<JsonConverter> { new PayloadConverter<TPayload>(), new IntegrationTelemetryDataConverter(), new MessageBatchDataConverter(), new ConfigurationKeyValueConverter() },
                });

            private static JsonSerializer CreateLogsSerializer() =>
                JsonSerializer.Create(new JsonSerializerSettings
                {
                    NullValueHandling = JsonTelemetryTransport.SerializerSettings.NullValueHandling,
                    ContractResolver = JsonTelemetryTransport.SerializerSettings.ContractResolver,
                    Converters = new List<JsonConverter> { new LogsConverter(), new IntegrationTelemetryDataConverter(), new MessageBatchDataConverter(), new ConfigurationKeyValueConverter() },
                });

            private static JsonSerializer CreateNullPayloadSerializer() =>
                JsonSerializer.Create(new JsonSerializerSettings
                {
                    NullValueHandling = JsonTelemetryTransport.SerializerSettings.NullValueHandling,
                    ContractResolver = JsonTelemetryTransport.SerializerSettings.ContractResolver,
                });
        }

        private class MessageBatchDataConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
                => objectType == typeof(MessageBatchData);

            // use the default serialization - it works fine
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => serializer.Serialize(writer, value);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var jsonObject = JObject.Load(reader);
                var requestType = jsonObject["request_type"]?.ToString();
                if (!TelemetryConverter.V2Serializers.TryGetValue(requestType!, out var innerSerializer))
                {
                    throw new Exception($"Unknown message batch telemetry request type {requestType}");
                }

                return new MessageBatchData(requestType, jsonObject["payload"]?.ToObject<IPayload>(innerSerializer));
            }
        }

        private class PayloadConverter<TPayload> : JsonConverter
        {
            public override bool CanConvert(Type objectType)
                => objectType == typeof(IPayload);

            // use the default serialization - it works fine
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => serializer.Serialize(writer, value);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                => serializer.Deserialize<TPayload>(reader);
        }

        private class LogsConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
                => objectType == typeof(IPayload);

            // use the default serialization - it works fine
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => serializer.Serialize(writer, value);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);

                return token.Type switch
                {
                    JTokenType.Object => token.ToObject<LogsPayload>(serializer),
                    JTokenType.Array => new LogsPayload(token.ToObject<LogMessageData[]>(serializer).ToList()),
                    _ => throw new JsonSerializationException($"Unexpected token type: {token.Type}")
                };
            }
        }

        private class IntegrationTelemetryDataConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(IntegrationTelemetryData);
            }

            // use the default serialization - it works fine
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => serializer.Serialize(writer, value);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // This is a pain, but for some reason Json.NET refuses to deserialize it otherwise
                string name = null;
                bool? enabled = null;
                bool? autoEnabled = null;
                string error = null;

                var contractResolver = (DefaultContractResolver)serializer.ContractResolver;
                var nameProperty = contractResolver.GetResolvedPropertyName(nameof(IntegrationTelemetryData.Name));
                var errorProperty = contractResolver.GetResolvedPropertyName(nameof(IntegrationTelemetryData.Error));
                var enabledProperty = contractResolver.GetResolvedPropertyName(nameof(IntegrationTelemetryData.Enabled));
                var autoEnabledProperty = contractResolver.GetResolvedPropertyName(nameof(IntegrationTelemetryData.AutoEnabled));

                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.PropertyName)
                    {
                        break;
                    }

                    var propertyName = (string)reader.Value;
                    if (!reader.Read())
                    {
                        continue;
                    }

                    if (nameProperty.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        name = serializer.Deserialize<string>(reader);
                        continue;
                    }

                    if (errorProperty.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        error = serializer.Deserialize<string>(reader);
                        continue;
                    }

                    if (enabledProperty.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        enabled = serializer.Deserialize<bool>(reader);
                        continue;
                    }

                    if (autoEnabledProperty.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        autoEnabled = serializer.Deserialize<bool>(reader);
                    }
                }

                if (name is null || enabled is null)
                {
                    throw new InvalidDataException($"Missing properties {nameProperty} and {enabledProperty} in serialized {nameof(IntegrationTelemetryData)}");
                }

                return new IntegrationTelemetryData(name, enabled.Value, autoEnabled, error);
            }
        }

        private class ConfigurationKeyValueConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(ConfigurationKeyValue);
            }

            // use the default serialization - it works fine
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => serializer.Serialize(writer, value);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // This is a pain, but for some reason Json.NET refuses to deserialize it otherwise
                var jo = JObject.Load(reader);

                var contractResolver = (DefaultContractResolver)serializer.ContractResolver;
                var name = jo[contractResolver.GetResolvedPropertyName(nameof(ConfigurationKeyValue.Name))]?.ToString();
                var jToken = jo[contractResolver.GetResolvedPropertyName(nameof(ConfigurationKeyValue.Value))];
                object value = jToken?.Type switch
                {
                    JTokenType.Null => null,
                    JTokenType.Boolean => jToken.Value<bool>(),
                    JTokenType.Integer => jToken.Value<int>(),
                    JTokenType.Float => jToken.Value<double>(),
                    _ => jToken?.ToString()
                };

                var origin = jo[contractResolver.GetResolvedPropertyName(nameof(ConfigurationKeyValue.Origin))]?.ToString();
                var seqId = jo[contractResolver.GetResolvedPropertyName(nameof(ConfigurationKeyValue.SeqId))]?.Value<long>();
                var error = jo[contractResolver.GetResolvedPropertyName(nameof(ConfigurationKeyValue.Error))];
                ErrorData? errorData = null;
                if (error is not null)
                {
                    var errorCode = error[contractResolver.GetResolvedPropertyName(nameof(ErrorData.Code))]?.Value<int>();
                    var errorMessage = error[contractResolver.GetResolvedPropertyName(nameof(ErrorData.Message))]?.ToString();
                    errorData = new ErrorData((TelemetryErrorCode)errorCode, errorMessage);
                }

                return ConfigurationKeyValue.Create(name, value, origin, seqId.Value, errorData);
            }
        }
    }
}
