// <copyright file="JsonHttpClientTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class JsonHttpClientTelemetryTransport : JsonTelemetryTransportBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JsonHttpClientTelemetryTransport>();
        private readonly HttpClient _httpClient;

        public JsonHttpClientTelemetryTransport(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            foreach (var defaultHeader in TelemetryHttpHeaderNames.DefaultHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(defaultHeader.Key, defaultHeader.Value);
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add(TelemetryConstants.ApiKeyHeader, apiKey);
            }
        }

        public override async Task PushTelemetry(TelemetryData data)
        {
            try
            {
                // have to buffer in memory so we know the content length
                var serializedData = SerializeTelemetry(data);

                var request = new HttpRequestMessage(HttpMethod.Post, TelemetryConstants.TelemetryPath)
                {
                    Content = new StringContent(serializedData, Encoding.UTF8, "application/json")
                };
                request.Headers.Add(TelemetryConstants.ApiVersionHeader, data.ApiVersion);
                request.Headers.Add(TelemetryConstants.RequestTypeHeader, data.RequestType);

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    Log.Debug("Telemetry sent successfully");
                }
                else
                {
                    Log.Debug("Error sending telemetry {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending telemetry data");
            }
        }
    }
}
#endif
