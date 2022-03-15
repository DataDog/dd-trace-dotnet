// <copyright file="JsonWebRequestTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class JsonWebRequestTelemetryTransport : JsonTelemetryTransportBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JsonWebRequestTelemetryTransport>();
        private readonly string _apiKey;
        private readonly Uri _endpoint;

        public JsonWebRequestTelemetryTransport(Uri baseEndpoint, string apiKey)
        {
            _apiKey = apiKey;
            _endpoint = new Uri(baseEndpoint, TelemetryConstants.TelemetryPath);
        }

        public override Task<TelemetryPushResult> PushTelemetryAsync(TelemetryData data)
        {
            return Task.Factory.StartNew(() => PushTelemetry(data), TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static bool IsFatalException(Exception ex)
        {
            return ex is SocketException
#if !NETFRAMEWORK
                       or WebException { InnerException: System.Net.Http.HttpRequestException { InnerException: SocketException } }
#endif
                       or WebException { Response: HttpWebResponse { StatusCode: HttpStatusCode.NotFound } }
                       or WebException { InnerException: SocketException };
        }

        private TelemetryPushResult PushTelemetry(TelemetryData data)
        {
            lock (this)
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

                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        request.Headers.Add(TelemetryConstants.ApiKeyHeader, _apiKey);
                    }

                    request.Headers.Add(TelemetryConstants.ApiVersionHeader, data.ApiVersion);
                    request.Headers.Add(TelemetryConstants.RequestTypeHeader, data.RequestType);

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(bytes, 0, bytes.Length);
                    }

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        var statusCode = (int)response.StatusCode;
                        switch (statusCode)
                        {
                            case >= 200 and < 300:
                                Log.Debug("Telemetry sent successfully");
                                return TelemetryPushResult.Success;
                            case 404:
                                Log.Debug("Error sending telemetry: 404. Disabling further telemetry, as endpoint not found", response.StatusCode);
                                return TelemetryPushResult.FatalError;
                            default:
                                Log.Debug("Error sending telemetry {StatusCode}", response.StatusCode);
                                return TelemetryPushResult.TransientFailure;
                        }
                    }
                }
                catch (Exception ex) when (IsFatalException(ex))
                {
                    Log.Warning(ex, "Error sending telemetry data, unable to communicate with endpoint");
                    return TelemetryPushResult.FatalError;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error sending telemetry data");
                    return TelemetryPushResult.TransientFailure;
                }
            }
        }
    }
}
