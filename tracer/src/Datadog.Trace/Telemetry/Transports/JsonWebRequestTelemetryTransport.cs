// <copyright file="JsonWebRequestTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class JsonWebRequestTelemetryTransport : JsonTelemetryTransportBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JsonWebRequestTelemetryTransport>();
        private readonly Uri _endpoint;

        public JsonWebRequestTelemetryTransport(Uri baseEndpoint)
        {
            _endpoint = new Uri(baseEndpoint, TelemetryConstants.TelemetryPath);
        }

        public override async Task PushTelemetry(TelemetryData data)
        {
            try
            {
                // have to buffer in memory so we know the content length
                var serializedData = SerializeTelemetry(data);
                var bytes = Encoding.UTF8.GetBytes(serializedData);

                var request = WebRequest.CreateHttp(_endpoint);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = bytes.Length;

                foreach (var defaultHeader in TelemetryHttpHeaderNames.DefaultHeaders)
                {
                    request.Headers.Add(defaultHeader.Key, defaultHeader.Value);
                }

                request.Headers.Add(TelemetryConstants.ApiVersionHeader, data.ApiVersion);
                request.Headers.Add(TelemetryConstants.RequestTypeHeader, data.RequestType);

                using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await requestStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }

                var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                var statusCode = (int)response.StatusCode;
                if (statusCode >= 200 && statusCode < 300)
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
