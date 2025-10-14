// <copyright file="JsonTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Telemetry.Transports
{
    internal abstract class JsonTelemetryTransport : ITelemetryTransport
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JsonTelemetryTransport>();
        internal static readonly JsonSerializerSettings SerializerSettings = new() { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy(), } };

        private readonly IApiRequestFactory _requestFactory;
        private readonly Uri _endpoint;
        private readonly string? _containerId;
        private readonly string? _entityId;
        private readonly bool _enableDebug;
        private readonly bool _compressPayload;

        protected JsonTelemetryTransport(IApiRequestFactory requestFactory, bool enableDebug, bool compressPayload)
        {
            _requestFactory = requestFactory;
            _enableDebug = enableDebug;
            _endpoint = _requestFactory.GetEndpoint(TelemetryConstants.TelemetryPath);
            _containerId = ContainerMetadata.GetContainerId();
            _entityId = ContainerMetadata.GetEntityId();
            _compressPayload = compressPayload;
        }

        protected string GetEndpointInfo() => _requestFactory.Info(_endpoint);

        public async Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            var endpointMetricTag = GetEndpointMetricTag();

            try
            {
                // have to buffer in memory so we know the content length
                var serializedData = SerializeTelemetry(data);
                var bytes = Encoding.UTF8.GetBytes(serializedData);
                var request = _requestFactory.Create(_endpoint);

                if (_compressPayload)
                {
                    bytes = CompressGzip(bytes);
                }

                request.AddHeader(TelemetryConstants.ApiVersionHeader, data.ApiVersion);
                request.AddHeader(TelemetryConstants.RequestTypeHeader, data.RequestType);

                if (_enableDebug)
                {
                    request.AddHeader(TelemetryConstants.DebugHeader, "true");
                }

                if (_containerId is not null)
                {
                    request.AddHeader(TelemetryConstants.ContainerIdHeader, _containerId);
                }

                if (_entityId is not null)
                {
                    request.AddHeader(TelemetryConstants.EntityIdHeader, _entityId);
                }

                TelemetryFactory.Metrics.RecordCountTelemetryApiRequests(endpointMetricTag);

                using var response = await request.PostAsync(new ArraySegment<byte>(bytes), "application/json", _compressPayload ? "gzip" : null).ConfigureAwait(false);
                TelemetryFactory.Metrics.RecordCountTelemetryApiResponses(endpointMetricTag, response.GetTelemetryStatusCodeMetricTag());
                if (response.StatusCode is >= 200 and < 300)
                {
                    Log.Debug("{Compress} Telemetry sent successfully", _compressPayload ? "Compressed" : "Non compressed");
                    return TelemetryPushResult.Success;
                }

                TelemetryFactory.Metrics.RecordCountTelemetryApiErrors(endpointMetricTag, MetricTags.ApiError.StatusCode);

                if (response.StatusCode == 404)
                {
                    Log.Debug("Error sending {Compress} telemetry: 404. Disabling further telemetry, as endpoint '{Endpoint}' not found", _compressPayload ? "Compressed" : "Non compressed", GetEndpointInfo());
                    return TelemetryPushResult.FatalError;
                }

                Log.Debug<string, string, int>("Error sending {Compress} telemetry to '{Endpoint}' {StatusCode} ", _compressPayload ? "Compressed" : "Non compressed", GetEndpointInfo(), response.StatusCode);
                return TelemetryPushResult.TransientFailure;
            }
            catch (Exception ex) when (IsFatalException(ex))
            {
                Log.Information(ex, "Error sending {Compress} telemetry data, unable to communicate with '{Endpoint}'", _compressPayload ? "Compressed" : "Non compressed", GetEndpointInfo());
                var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                TelemetryFactory.Metrics.RecordCountTelemetryApiErrors(endpointMetricTag, tag);
                return TelemetryPushResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Error sending {Compress} telemetry data to '{Endpoint}'", _compressPayload ? "Compressed" : "Non compressed", GetEndpointInfo());
                var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                TelemetryFactory.Metrics.RecordCountTelemetryApiErrors(endpointMetricTag, tag);
                return TelemetryPushResult.TransientFailure;
            }
        }

        public abstract string GetTransportInfo();

        // Internal for testing
        internal static string SerializeTelemetry<T>(T data) => JsonConvert.SerializeObject(data, Formatting.None, SerializerSettings);

        protected abstract MetricTags.TelemetryEndpoint GetEndpointMetricTag();

        private static byte[] CompressGzip(byte[] input)
        {
            using var output = new MemoryStream();

            using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
            {
                gzip.Write(input, 0, input.Length);
            }

            return output.ToArray();
        }

        private static bool IsFatalException(Exception ex)
        {
            return ex.IsSocketException()
                || ex is WebException { Response: HttpWebResponse { StatusCode: HttpStatusCode.NotFound } };
        }
    }
}
