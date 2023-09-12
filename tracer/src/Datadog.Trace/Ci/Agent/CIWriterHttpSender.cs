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

        private async Task<int> SendPayloadAsync<T>(EventPlatformPayload payload, Func<IApiRequest, EventPlatformPayload, T, Task<IApiResponse>> senderFunc, T state)
        {
            // retry up to 5 times with exponential back-off
            const int retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds
            var url = payload.Url;

            while (true)
            {
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
                    return -1;
                }

                var statusCode = -1;
                var isFinalTry = retryCount >= retryLimit;
                Exception exception = null;

                try
                {
                    statusCode = await SendPayloadAsync(senderFunc, request, payload, state, isFinalTry).ConfigureAwait(false);
                }
                catch (MultipartApiRequestNotSupported mReqEx)
                {
                    Log.Error(mReqEx, "Error trying to send a multipart request to: {Url}", url.ToString());
                    return statusCode;
                }
                catch (Exception ex)
                {
                    exception = ex;

                    if (_isDebugEnabled)
                    {
                        if (ex.InnerException is InvalidOperationException ioe)
                        {
                            Log.Error<string>(ex, "An error occurred while sending events to {AgentEndpoint}", _apiRequestFactory.Info(url));
                            return statusCode;
                        }
                    }
                }

                // Error handling block
                if (IsStatusCodeError(statusCode))
                {
                    if (isFinalTry)
                    {
                        // stop retrying
                        Log.Error<int, string, int>(exception, "An error occurred while sending events after {Retries} retries to {AgentEndpoint} | StatusCode: {StatusCode}", retryCount, _apiRequestFactory.Info(url), statusCode);
                        return statusCode;
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
                return statusCode;
            }
        }

        private async Task SendPayloadAsync(CIVisibilityProtocolPayload payload)
        {
            ArraySegment<byte> payloadArraySegment;
            MemoryStream agentlessMemoryStream = null;
            TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadRequests(payload.TelemetryEndpoint);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadEventsCount(payload.TelemetryEndpoint, payload.Count);

            try
            {
                var sw = Stopwatch.StartNew();

                if (!payload.UseEvpProxy)
                {
                    // If we are in agentless mode (no EVP Proxy) then we use gzip compression, supported by the intake
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

                TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointEventsSerializationMs(payload.TelemetryEndpoint, sw.Elapsed.TotalMilliseconds);
                sw.Restart();

                var statusCode = await SendPayloadAsync(
                        payload,
                        static (request, payload, payloadBytes) => request.PostAsync(payloadBytes, MimeTypes.MsgPack, payload.UseEvpProxy ? null : "gzip"),
                        payloadArraySegment)
                   .ConfigureAwait(false);

                TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadRequestsMs(payload.TelemetryEndpoint, sw.Elapsed.TotalMilliseconds);
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadBytes(payload.TelemetryEndpoint, payloadArraySegment.Count);
                if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadRequestsErrors(payload.TelemetryEndpoint, errorType);
                }
            }
            finally
            {
                agentlessMemoryStream?.Dispose();
            }
        }

        private async Task SendPayloadAsync(MultipartPayload payload)
        {
            var sw = Stopwatch.StartNew();
            var payloadArray = payload.ToArray();
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointEventsSerializationMs(payload.TelemetryEndpoint, sw.Elapsed.TotalMilliseconds);
            var payloadBytes = 0;
            foreach (var multipartFormItem in payloadArray)
            {
                payloadBytes += multipartFormItem.ContentInBytes?.Count ?? 0;
            }

            TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadRequests(payload.TelemetryEndpoint);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadBytes(payload.TelemetryEndpoint, payloadBytes);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadEventsCount(payload.TelemetryEndpoint, payload.Count);

            sw.Reset();
            Log.Debug<int>("Sending {Count} multipart items...", payload.Count);
            var statusCode = await SendPayloadAsync(
                payload,
                static (request, payload, payloadArray) =>
                {
                    if (request is IMultipartApiRequest multipartRequest)
                    {
                        return multipartRequest.PostAsync(payloadArray);
                    }

                    MultipartApiRequestNotSupported.Throw();
                    return Task.FromResult<IApiResponse>(null);
                },
                payloadArray).ConfigureAwait(false);

            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointPayloadRequestsMs(payload.TelemetryEndpoint, sw.Elapsed.TotalMilliseconds);
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityEndpointPayloadRequestsErrors(payload.TelemetryEndpoint, errorType);
            }
        }

        private class MultipartApiRequestNotSupported : NotSupportedException
        {
            public MultipartApiRequestNotSupported()
                : base("Sender doesn't support IMultipartApiRequest.")
            {
            }

            [DebuggerHidden]
            [DoesNotReturn]
            public static void Throw() => throw new MultipartApiRequestNotSupported();
        }
    }
}
