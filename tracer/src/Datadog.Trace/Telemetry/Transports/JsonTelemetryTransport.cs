// <copyright file="JsonTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.SourceGenerators;
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
        private readonly ContainerMetadata _containerMetadata;
        private readonly bool _enableDebug;
        private readonly bool _telemetryGzipCompressionEnabled;
        private readonly string _telemetryCompressionMethod;

        protected JsonTelemetryTransport(IApiRequestFactory requestFactory, bool enableDebug, string telemetryCompressionMethod, ContainerMetadata containerMetadata)
        {
            _requestFactory = requestFactory;
            _enableDebug = enableDebug;
            _endpoint = _requestFactory.GetEndpoint(TelemetryConstants.TelemetryPath);
            _containerMetadata = containerMetadata;
            _telemetryGzipCompressionEnabled = telemetryCompressionMethod.Equals("gzip", StringComparison.OrdinalIgnoreCase);
            _telemetryCompressionMethod = _telemetryGzipCompressionEnabled ? "gzip" : "uncompressed";
        }

        protected string GetEndpointInfo() => _requestFactory.Info(_endpoint);

        public async Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            var endpointMetricTag = GetEndpointMetricTag();

            try
            {
                byte[] bytes;

                if (_telemetryGzipCompressionEnabled)
                {
                    bytes = SerializeTelemetryWithGzip(data);
                }
                else
                {
                    bytes = Encoding.UTF8.GetBytes(SerializeTelemetry(data));
                }

                var request = _requestFactory.Create(_endpoint);
                request.AddHeader(TelemetryConstants.ApiVersionHeader, data.ApiVersion);
                request.AddHeader(TelemetryConstants.RequestTypeHeader, data.RequestType);

                if (_enableDebug)
                {
                    request.AddHeader(TelemetryConstants.DebugHeader, "true");
                }

                request.AddContainerMetadataHeaders(_containerMetadata);

                TelemetryFactory.Metrics.RecordCountTelemetryApiRequests(endpointMetricTag);

                using var response = await request.PostAsync(new ArraySegment<byte>(bytes), "application/json", _telemetryGzipCompressionEnabled ? "gzip" : null).ConfigureAwait(false);
                TelemetryFactory.Metrics.RecordCountTelemetryApiResponses(endpointMetricTag, response.GetTelemetryStatusCodeMetricTag());
                if (response.StatusCode is >= 200 and < 300)
                {
                    Log.Debug("Telemetry sent successfully. Compression {Compression}", _telemetryCompressionMethod);
                    return TelemetryPushResult.Success;
                }

                TelemetryFactory.Metrics.RecordCountTelemetryApiErrors(endpointMetricTag, MetricTags.ApiError.StatusCode);

                if (response.StatusCode == 404)
                {
                    Log.Debug("Error sending telemetry: 404. Disabling further telemetry, as endpoint '{Endpoint}' not found. Compression {Compression}", GetEndpointInfo(), _telemetryCompressionMethod);
                    return TelemetryPushResult.FatalError;
                }

                Log.Debug<string, int, string>("Error sending telemetry to '{Endpoint}' {StatusCode} . Compression {Compression}", GetEndpointInfo(), response.StatusCode, _telemetryCompressionMethod);
                return TelemetryPushResult.TransientFailure;
            }
            catch (Exception ex) when (IsFatalException(ex))
            {
                Log.Information(ex, "Error sending telemetry data, unable to communicate with '{Endpoint}'. Compression {Compression}", GetEndpointInfo(), _telemetryCompressionMethod);
                var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                TelemetryFactory.Metrics.RecordCountTelemetryApiErrors(endpointMetricTag, tag);
                return TelemetryPushResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Error sending telemetry data to '{Endpoint}'. Compression {Compression}", GetEndpointInfo(), _telemetryCompressionMethod);
                var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                TelemetryFactory.Metrics.RecordCountTelemetryApiErrors(endpointMetricTag, tag);
                return TelemetryPushResult.TransientFailure;
            }
        }

        public abstract string GetTransportInfo();

        [TestingAndPrivateOnly]
        internal static string SerializeTelemetry<T>(T data) => JsonConvert.SerializeObject(data, Formatting.None, SerializerSettings);

        protected abstract MetricTags.TelemetryEndpoint GetEndpointMetricTag();

        internal static byte[] SerializeTelemetryWithGzip<T>(T data)
        {
            using var memStream = new MemoryStream();
            using (var zipStream = new GZipStream(memStream, CompressionMode.Compress, true))
            {
                using var streamWriter = new StreamWriter(zipStream);
                using var jsonWriter = new JsonTextWriter(streamWriter);
                var serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy(), }, Formatting = Formatting.None };

                serializer.Serialize(jsonWriter, data);
            }

            return memStream.ToArray();
        }

        private static bool IsFatalException(Exception ex)
        {
            return ex.IsSocketException()
                || ex is WebException { Response: HttpWebResponse { StatusCode: HttpStatusCode.NotFound } };
        }
    }
}
