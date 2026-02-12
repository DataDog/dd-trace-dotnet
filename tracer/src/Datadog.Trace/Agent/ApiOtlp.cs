// <copyright file="ApiOtlp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Agent
{
    internal sealed class ApiOtlp : IApi
    {
        internal const string FailedToSendMessageTemplate = "An error occurred while sending data to the agent at {OtlpTracesEndpoint}. If the error isn't transient, please check https://docs.datadoghq.com/tracing/troubleshooting/connection_errors/?code-lang=dotnet for guidance.";

        private static readonly IDatadogLogger StaticLog = DatadogLogging.GetLoggerFor<ApiOtlp>();

        private readonly IDatadogLogger _log;
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly TracesEncoding _tracesEncoding;
        private readonly Uri _tracesEndpoint;
        private readonly Uri _statsEndpoint; // This endpoint is passed for the _sendStats callback, but otherwise unused
        private readonly SendCallback<SendStatsState> _sendStats;
        private readonly SendCallback<SendTracesState> _sendTraces;
#if NET6_0_OR_GREATER
        private readonly Datadog.Trace.OpenTelemetry.Metrics.OtlpExporter _metricsExporter;
#endif

        public ApiOtlp(
            IApiRequestFactory apiRequestFactory,
            Uri tracesEndpoint,
            TracesEncoding tracesEncoding,
            OtlpProtocol statsProtocol,
            Uri statsEndpoint,
            KeyValuePair<string, string>[] statsHeaders,
            int statsTimeoutMs,
            IDatadogLogger log = null)
        {
            // optionally injecting a log instance in here for testing purposes
            _log = log ?? StaticLog;
            _log.Debug("Creating new ApiOtlp");
            _sendStats = SendStatsAsyncImpl;
            _sendTraces = SendTracesAsyncImpl;

            _apiRequestFactory = apiRequestFactory;
            _tracesEncoding = tracesEncoding;
            _tracesEndpoint = tracesEndpoint;
            _statsEndpoint = statsEndpoint;
            _log.Debug("Using traces endpoint {TracesEndpoint}", _tracesEndpoint.ToString());

#if NET6_0_OR_GREATER
            _metricsExporter = new Datadog.Trace.OpenTelemetry.Metrics.OtlpExporter(Tracer.Instance.Settings, statsProtocol, statsEndpoint, statsHeaders, statsTimeoutMs);
#endif
        }

        private delegate Task<SendResult> SendCallback<T>(IApiRequest request, bool isFinalTry, T state);

        private enum SendResult
        {
            Success,
            Failed_CanRetry,
            Failed_DontRetry,
        }

        public TracesEncoding TracesEncoding => _tracesEncoding;

        public Task<bool> Ping() => Task.FromResult(true);

        public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        {
            _log.Debug("Sending trace stats to the OTLP Metrics endpoint.");

            var state = new SendStatsState(stats, bucketDuration);

            return SendWithRetry(_statsEndpoint, _sendStats, state);
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled = true)
        {
            _log.Debug<int>("Sending {Count} traces to the OTLP Traces endpoint.", numberOfTraces);

            var state = new SendTracesState(traces, numberOfTraces, statsComputationEnabled, numberOfDroppedP0Traces, numberOfDroppedP0Spans, apmTracingEnabled);

            return SendWithRetry(_tracesEndpoint, _sendTraces, state);
        }

        private async Task<bool> SendWithRetry<T>(Uri endpoint, SendCallback<T> callback, T state)
        {
            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(endpoint);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "An error occurred while generating http request to send data to the agent at {AgentEndpoint}", _apiRequestFactory.Info(endpoint));
                    return false;
                }

                var success = SendResult.Failed_DontRetry;
                Exception exception = null;
                bool isFinalTry = retryCount >= retryLimit;

                try
                {
                    success = await callback(request, isFinalTry, state).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exception = ex;
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        _log.Error(ex, "An error occurred while sending data to the agent at {AgentEndpoint}", _apiRequestFactory.Info(endpoint));
                        return false;
                    }
#endif
                }

                // Error handling block
                if (success != SendResult.Success)
                {
                    if (isFinalTry || success == SendResult.Failed_DontRetry)
                    {
                        // stop retrying
                        _log.ErrorSkipTelemetry(exception, FailedToSendMessageTemplate, _apiRequestFactory.Info(endpoint));
                        return false;
                    }
                    else if (_log.IsEnabled(LogEventLevel.Debug))
                    {
                        _log.Debug(exception, "An error occurred while sending data to the agent at {AgentEndpoint}. Retrying.", _apiRequestFactory.Info(endpoint));
                    }

                    // Before retry delay
                    if (exception.IsSocketException())
                    {
                        _log.Debug(exception, "Unable to communicate with the trace agent at {AgentEndpoint}", _apiRequestFactory.Info(endpoint));
                    }

                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    continue;
                }

                return true;
            }
        }

#if NET6_0_OR_GREATER
        private async Task<SendResult> SendStatsAsyncImpl(IApiRequest request, bool isFinalTry, SendStatsState state)
        {
            bool success = false;
            IApiResponse response = null;

            var endTime = DateTimeOffset.UtcNow;
            var metrics = OtlpMapper.ConvertToOtlpMetrics(state.Stats, endTime);
            Datadog.Trace.OpenTelemetry.ExportResult exportResult;

            try
            {
                // TODO: Telemetry - Record OTLP Metrics API requests for APM trace stats
                exportResult = await _metricsExporter.ExportAsync(metrics).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // TODO: Telemetry - Record OTLP Metrics API errors for APM trace stats
                throw;
            }

            // TelemetryFactory.Metrics.RecordCountStatsApiResponses(response.GetTelemetryStatusCodeMetricTag());
            if (exportResult == Datadog.Trace.OpenTelemetry.ExportResult.Success)
            {
                success = true;
            }

            if (success)
            {
                _log.Debug("Successfully sent APM trace stats to the OTLP metrics endpoint.");
            }
            else
            {
                // TODO: Telemetry - Record OTLP Metrics API errors for APM trace stats
            }

            response?.Dispose();
            return success ? SendResult.Success : SendResult.Failed_DontRetry;
        }
#else
        private Task<SendResult> SendStatsAsyncImpl(IApiRequest request, bool isFinalTry, SendStatsState state)
        {
            _log.Debug("Sending APM trace stats is currently only supported on .NET 6+");
            return Task.FromResult(SendResult.Success);
        }
#endif

        private async Task<SendResult> SendTracesAsyncImpl(IApiRequest request, bool finalTry, SendTracesState state)
        {
            IApiResponse response = null;

            var traces = state.Traces;
            var numberOfTraces = state.NumberOfTraces;

            // TODO: Determine if we need to send the following information somehow:
            // - DroppedP0Traces
            // - DroppedP0Spans

            // TODO: Determine if we need to do anything special when !apmTracingEnabled
            // My guess is no, since we would originally send the header Datadog-Client-Computed-Stats=true
            // to disable the Datadog agent from generating APM Trace Stats from this traces payload

            try
            {
                try
                {
                    // TODO: Telemetry - Record OTLP Traces API submissions
                    // TODO: Add more precise logic for "application/x-protobuf" vs "application/json"
                    response = await request.PostAsync(traces, MimeTypes.Json).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // count only network/infrastructure errors, not valid responses with error status codes
                    // (which are handled below)
                    // TODO: Telemetry - Record OTLP Traces API errors
                    throw;
                }

                // TODO: Increment OTLP Trace responses

                // Per the OTLP specification, an OTLP request with either SUCCESS or PARTIAL_SUCCESS MUST have a 200 Status Code
                // The following HTTP response status codes SHOULD be retried:
                // StatusCode 429 -> Too Many Requests
                // StatusCode 502 -> Bad Gateway
                // StatusCode 503 -> Service Unavailable
                // StatusCode 504 -> Gateway Timeout
                //
                // All other status codes MUST NOT be retried.
                if (response.StatusCode == 200)
                {
                    _log.Debug<int>("Successfully sent {Count} traces to the OTLP traces endpoint.", numberOfTraces);
                }
                else if (response.StatusCode != 429 && response.StatusCode != 502 && response.StatusCode != 503 && response.StatusCode != 504)
                {
                    try
                    {
                        string responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                        _log.Error<int, string>("Failed to submit traces with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                    }
                    catch (Exception ex)
                    {
                        _log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                    }

                    // TODO: Telemetry - Record OTLP Traces API error with status code
                    return SendResult.Failed_DontRetry;
                }
                else
                {
                    if (finalTry)
                    {
                        try
                        {
                            string responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                            _log.Error<int, string>("Failed to submit traces with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                        }
                        catch (Exception ex)
                        {
                            _log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                        }
                    }
                    else
                    {
                        // TODO: Should back-propagate retryAfter value
                        string retryAfter = response.StatusCode == 429 || response.StatusCode == 503 ? response.GetHeader("Retry-After") : null;
                        _log.Debug<int, string>("Failed to submit {Count} traces. OTLP response header Retry-After={RetryAfter}", numberOfTraces, retryAfter ?? "unspecified");
                    }

                    // TODO: Telemetry - Record OTLP Traces API error with status code
                    return finalTry ? SendResult.Failed_DontRetry : SendResult.Failed_CanRetry;
                }

                if (response.StatusCode == 429 || response.StatusCode == 413 || response.StatusCode == 408)
                {
                    var retryAfter = response.GetHeader("Retry-After");
                    _log.Debug<int, string>("Failed to submit {Count} traces. Agent responded with 429 Too Many Requests, retry after {RetryAfter}", numberOfTraces, retryAfter ?? "unspecified");
                    return SendResult.Failed_DontRetry;
                }
            }
            finally
            {
                response?.Dispose();
            }

            return SendResult.Success;
        }

        private readonly struct SendTracesState
        {
            public readonly ArraySegment<byte> Traces;
            public readonly int NumberOfTraces;
            public readonly bool StatsComputationEnabled;
            public readonly long NumberOfDroppedP0Traces;
            public readonly long NumberOfDroppedP0Spans;
            public readonly bool ApmTracingEnabled;

            public SendTracesState(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled)
            {
                Traces = traces;
                NumberOfTraces = numberOfTraces;
                StatsComputationEnabled = statsComputationEnabled;
                NumberOfDroppedP0Traces = numberOfDroppedP0Traces;
                NumberOfDroppedP0Spans = numberOfDroppedP0Spans;
                ApmTracingEnabled = apmTracingEnabled;
            }
        }

        private readonly struct SendStatsState
        {
            public readonly StatsBuffer Stats;
            public readonly long BucketDuration;

            public SendStatsState(StatsBuffer stats, long bucketDuration)
            {
                Stats = stats;
                BucketDuration = bucketDuration;
            }
        }
    }
}
