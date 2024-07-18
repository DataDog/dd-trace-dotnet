// <copyright file="CIWriterHttpSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Agent
{
    internal sealed class CIWriterHttpSender : ICIVisibilityProtocolWriterSender
    {
        private const string ApiKeyHeader = "dd-api-key";
        private const string EvpSubdomainHeader = "X-Datadog-EVP-Subdomain";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIWriterHttpSender>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly bool _isDebugEnabled;

        public CIWriterHttpSender(IApiRequestFactory apiRequestFactory)
        {
            _apiRequestFactory = apiRequestFactory;
            _isDebugEnabled = GlobalSettings.Instance.DebugEnabledInternal;
            Log.Information("CIWriterHttpSender Initialized.");
        }

        public Task SendPayloadAsync(EventPlatformPayload payload)
        {
            switch (payload)
            {
                case CIVisibilityProtocolPayload ciVisibilityProtocolPayload:
                    return SendPayloadAsync(ciVisibilityProtocolPayload);
                case MultipartPayload multipartPayload:
                    return SendPayloadAsync(multipartPayload);
                default:
                    Util.ThrowHelper.ThrowNotSupportedException("Payload is not supported.");
                    return Task.FromException(new NotSupportedException("Payload is not supported."));
            }
        }

        private static bool IsStatusCodeError(int statusCode) => statusCode is < 200 or >= 300;

        private static async Task<int> SendPayloadAsync<T>(Func<IApiRequest, EventPlatformPayload, T, Task<IApiResponse>> senderFunc, IApiRequest request, EventPlatformPayload payload, T state, bool finalTry)
        {
            IApiResponse response = null;
            int statusCode;

            try
            {
                response = await senderFunc(request, payload, state).ConfigureAwait(false);
                statusCode = response.StatusCode;

                // Attempt a retry if the status code is not SUCCESS
                if (IsStatusCodeError(statusCode))
                {
                    if (finalTry)
                    {
                        try
                        {
                            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                            Log.Error<int, string>("Failed to submit events with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                        }
                        catch (Exception ex)
                        {
                            Log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
            }

            return statusCode;
        }

        private async Task SendPayloadAsync<T>(EventPlatformPayload payload, Func<IApiRequest, EventPlatformPayload, T, Task<IApiResponse>> senderFunc, T state)
        {
            // retry up to 5 times with exponential back-off
            const int retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds
            var url = payload.Url;
            var sw = Stopwatch.StartNew();

            while (true)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadRequests(payload.TelemetryEndpointAndCompression);

                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(url);
                    if (payload.UseEvpProxy)
                    {
                        request.AddHeader(EvpSubdomainHeader, payload.EventPlatformSubdomain);
                    }
                    else
                    {
                        request.AddHeader(ApiKeyHeader, CIVisibility.Settings.ApiKey);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while generating http request to send events to {AgentEndpoint}", _apiRequestFactory.Info(url));
                    return;
                }

                var statusCode = -1;
                var isFinalTry = retryCount >= retryLimit;
                Exception exception = null;

                sw.Restart();
                try
                {
                    statusCode = await SendPayloadAsync(senderFunc, request, payload, state, isFinalTry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exception = ex;

                    if (_isDebugEnabled)
                    {
                        if (ex.InnerException is InvalidOperationException ioe)
                        {
                            Log.Error<string>(ex, "An error occurred while sending events to {AgentEndpoint}", _apiRequestFactory.Info(url));
                            TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadDropped(payload.TelemetryEndpoint);
                            return;
                        }
                    }
                }
                finally
                {
                    TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadRequestsMs(payload.TelemetryEndpoint, sw.Elapsed.TotalMilliseconds);
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadRequestsErrors(payload.TelemetryEndpoint, errorType);
                    }
                }

                // Error handling block
                if (IsStatusCodeError(statusCode))
                {
                    if (isFinalTry)
                    {
                        // stop retrying
                        Log.Error<int, string, int>(exception, "An error occurred while sending events after {Retries} retries to {AgentEndpoint} | StatusCode: {StatusCode}", retryCount, _apiRequestFactory.Info(url), statusCode);
                        TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadDropped(payload.TelemetryEndpoint);
                        return;
                    }

                    // Before retry delay
                    if (exception.IsSocketException())
                    {
                        Log.Debug(exception, "Unable to communicate with {AgentEndpoint}", _apiRequestFactory.Info(url));
                    }

                    // Execute retry delay
                    Log.Debug<string, int>(exception, "An error occurred while sending events to {AgentEndpoint} | StatusCode: {StatusCode}", _apiRequestFactory.Info(url), statusCode);
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    continue;
                }

                Log.Debug<string>("Successfully sent events to {AgentEndpoint}", _apiRequestFactory.Info(url));
                return;
            }
        }

        private async Task SendPayloadAsync(CIVisibilityProtocolPayload payload)
        {
            ArraySegment<byte> payloadArraySegment;
            MemoryStream agentlessMemoryStream = null;
            try
            {
                if (payload.UseGZip)
                {
                    agentlessMemoryStream = new MemoryStream();
                    int uncompressedSize;
                    using (var gzipStream = new GZipStream(agentlessMemoryStream, CompressionLevel.Fastest, true))
                    {
                        uncompressedSize = payload.WriteTo(gzipStream);
                    }

                    agentlessMemoryStream.TryGetBuffer(out payloadArraySegment);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug<int, string, string>("Sending ({NumberOfTraces} events) {BytesValue} bytes... ({Uncompressed} bytes uncompressed)", payload.Count, payloadArraySegment.Count.ToString("N0"), uncompressedSize.ToString("N0"));
                    }
                }
                else
                {
                    payloadArraySegment = new ArraySegment<byte>(payload.ToArray());
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug<int, string>("Sending ({NumberOfTraces} events) {BytesValue} bytes...", payload.Count, payloadArraySegment.Count.ToString("N0"));
                    }
                }

                TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadEventsCount(payload.TelemetryEndpoint, payload.Count);
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadBytes(payload.TelemetryEndpoint, payloadArraySegment.Count);

                await SendPayloadAsync(
                        payload,
                        static (request, payload, payloadBytes) => request.PostAsync(payloadBytes, MimeTypes.MsgPack, payload.UseGZip ? "gzip" : null),
                        payloadArraySegment)
                   .ConfigureAwait(false);
            }
            finally
            {
                agentlessMemoryStream?.Dispose();
            }
        }

        private async Task SendPayloadAsync(MultipartPayload payload)
        {
            var payloadArray = payload.ToArray();
            var payloadBytes = 0;
            foreach (var multipartFormItem in payloadArray)
            {
                payloadBytes += multipartFormItem.ContentInBytes?.Count ?? 0;
            }

            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadBytes(payload.TelemetryEndpoint, payloadBytes);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadEventsCount(payload.TelemetryEndpoint, payload.Count);

            Log.Debug<int>("Sending {Count} multipart items...", payload.Count);
            await SendPayloadAsync(
                payload,
                static (request, payload, payloadArray) => request.PostAsync(payloadArray, payload.UseGZip ? MultipartCompression.GZip : MultipartCompression.None),
                payloadArray).ConfigureAwait(false);
        }
    }
}
