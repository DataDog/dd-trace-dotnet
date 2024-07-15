// <copyright file="Api.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.4/traces";
        private const string StatsPath = "/v0.6/stats";
        internal const string FailedToSendMessageTemplate = "An error occurred while sending data to the agent at {AgentEndpoint}. If the error isn't transient, please check https://docs.datadoghq.com/tracing/troubleshooting/connection_errors/?code-lang=dotnet for guidance.";

        private static readonly IDatadogLogger StaticLog = DatadogLogging.GetLoggerFor<Api>();

        private readonly IDatadogLogger _log;
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDogStatsd _statsd;
        private readonly string _containerId;
        private readonly string _entityId;
        private readonly Uri _tracesEndpoint;
        private readonly Uri _statsEndpoint;
        private readonly Action<Dictionary<string, float>> _updateSampleRates;
        private readonly bool _partialFlushEnabled;
        private readonly SendCallback<SendStatsState> _sendStats;
        private readonly SendCallback<SendTracesState> _sendTraces;
        private string _cachedResponse;
        private string _agentVersion;

        public Api(
            IApiRequestFactory apiRequestFactory,
            IDogStatsd statsd,
            Action<Dictionary<string, float>> updateSampleRates,
            bool partialFlushEnabled,
            IDatadogLogger log = null)
        {
            // optionally injecting a log instance in here for testing purposes
            _log = log ?? StaticLog;
            _log.Debug("Creating new Api");
            _sendStats = SendStatsAsyncImpl;
            _sendTraces = SendTracesAsyncImpl;
            _updateSampleRates = updateSampleRates;
            _statsd = statsd;
            _containerId = ContainerMetadata.GetContainerId();
            _entityId = ContainerMetadata.GetEntityId();
            _apiRequestFactory = apiRequestFactory;
            _partialFlushEnabled = partialFlushEnabled;
            _tracesEndpoint = _apiRequestFactory.GetEndpoint(TracesPath);
            _log.Debug("Using traces endpoint {TracesEndpoint}", _tracesEndpoint.ToString());
            _statsEndpoint = _apiRequestFactory.GetEndpoint(StatsPath);
            _log.Debug("Using stats endpoint {StatsEndpoint}", _statsEndpoint.ToString());
        }

        private delegate Task<SendResult> SendCallback<T>(IApiRequest request, bool isFinalTry, T state);

        private enum SendResult
        {
            Success,
            Failed_CanRetry,
            Failed_DontRetry,
        }

        public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        {
            _log.Debug("Sending stats to the Datadog Agent.");

            var state = new SendStatsState(stats, bucketDuration);

            return SendWithRetry(_statsEndpoint, _sendStats, state);
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
        {
            _log.Debug<int>("Sending {Count} traces to the Datadog Agent.", numberOfTraces);

            var state = new SendTracesState(traces, numberOfTraces, statsComputationEnabled, numberOfDroppedP0Traces, numberOfDroppedP0Spans, appsecStandaloneEnabled);

            return SendWithRetry(_tracesEndpoint, _sendTraces, state);
        }

        // internal for testing
        internal bool LogPartialFlushWarningIfRequired(string agentVersion)
        {
            if (agentVersion != _agentVersion)
            {
                _agentVersion = agentVersion;

                if (_partialFlushEnabled)
                {
                    if (!Version.TryParse(agentVersion, out var parsedVersion) || parsedVersion < new Version(7, 26, 0))
                    {
                        var detectedVersion = string.IsNullOrEmpty(agentVersion) ? "{detection failed}" : agentVersion;

                        _log.Warning("DATADOG TRACER DIAGNOSTICS - Partial flush should only be enabled with agent 7.26.0+ (detected version: {Version})", detectedVersion);
                        return true;
                    }
                }
            }

            return false;
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
                        _log.Error(exception, FailedToSendMessageTemplate, _apiRequestFactory.Info(endpoint));
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

        private async Task<SendResult> SendStatsAsyncImpl(IApiRequest request, bool isFinalTry, SendStatsState state)
        {
            bool success = false;
            IApiResponse response = null;

            // Set additional headers
            if (_containerId != null)
            {
                request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
            }

            if (_entityId != null)
            {
                request.AddHeader(AgentHttpHeaderNames.EntityId, _entityId);
            }

            using var stream = new MemoryStream();
            state.Stats.Serialize(stream, state.BucketDuration);

            var buffer = stream.GetBuffer();

            try
            {
                try
                {
                    TelemetryFactory.Metrics.RecordCountStatsApiRequests();
                    response = await request.PostAsync(new ArraySegment<byte>(buffer, 0, (int)stream.Length), MimeTypes.MsgPack).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                    TelemetryFactory.Metrics.RecordCountStatsApiErrors(tag);
                    throw;
                }

                TelemetryFactory.Metrics.RecordCountStatsApiResponses(response.GetTelemetryStatusCodeMetricTag());

                if (response.StatusCode is >= 200 and < 300)
                {
                    success = true;
                }
                else if (isFinalTry)
                {
                    try
                    {
                        var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                        _log.Error<int, string>("Failed to submit stats. Status code {StatusCode}, message: {ResponseContent}", response.StatusCode, responseContent);
                    }
                    catch (Exception ex)
                    {
                        _log.Error<int>(ex, "Unable to read response for failed request. Status code {StatusCode}", response.StatusCode);
                    }
                }

                if (success)
                {
                    _log.Debug("Successfully sent stats to the Datadog Agent.");
                }
                else
                {
                    TelemetryFactory.Metrics.RecordCountStatsApiErrors(MetricTags.ApiError.StatusCode);
                }

                return success ? SendResult.Success :
                       isFinalTry ? SendResult.Failed_DontRetry : SendResult.Failed_CanRetry;
            }
            finally
            {
                response?.Dispose();
            }
        }

        private async Task<SendResult> SendTracesAsyncImpl(IApiRequest request, bool finalTry, SendTracesState state)
        {
            IApiResponse response = null;

            var traces = state.Traces;
            var numberOfTraces = state.NumberOfTraces;
            var statsComputationEnabled = state.StatsComputationEnabled;
            var numberOfDroppedP0Traces = state.NumberOfDroppedP0Traces;
            var numberOfDroppedP0Spans = state.NumberOfDroppedP0Spans;
            var appsecStandaloneEnabled = state.AppsecStandaloneEnabled;

            // Set additional headers
            request.AddHeader(AgentHttpHeaderNames.TraceCount, numberOfTraces.ToString());

            if (_containerId != null)
            {
                request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
            }

            if (_entityId != null)
            {
                request.AddHeader(AgentHttpHeaderNames.EntityId, _entityId);
            }

            if (statsComputationEnabled)
            {
                request.AddHeader(AgentHttpHeaderNames.StatsComputation, "true");
                request.AddHeader(AgentHttpHeaderNames.DroppedP0Traces, numberOfDroppedP0Traces.ToString());
                request.AddHeader(AgentHttpHeaderNames.DroppedP0Spans, numberOfDroppedP0Spans.ToString());
            }
            else if (appsecStandaloneEnabled)
            {
                request.AddHeader(AgentHttpHeaderNames.StatsComputation, "true");
            }

            try
            {
                try
                {
                    TelemetryFactory.Metrics.RecordCountTraceApiRequests();
                    _statsd?.Increment(TracerMetricNames.Api.Requests);
                    response = await request.PostAsync(traces, MimeTypes.MsgPack).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // count only network/infrastructure errors, not valid responses with error status codes
                    // (which are handled below)
                    var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                    TelemetryFactory.Metrics.RecordCountTraceApiErrors(tag);
                    _statsd?.Increment(TracerMetricNames.Api.Errors);
                    throw;
                }

                if (_statsd != null)
                {
                    // don't bother creating the tags array if trace metrics are disabled
                    string[] tags = { $"status:{response.StatusCode}" };

                    // count every response, grouped by status code
                    _statsd?.Increment(TracerMetricNames.Api.Responses, tags: tags);
                }

                TelemetryFactory.Metrics.RecordCountTraceApiResponses(response.GetTelemetryStatusCodeMetricTag());

                // A change on the agent changed the response when payloads are dropped from being 200 to 429
                // This change would cause us to start retrying these failed requests unlike before.
                // Since the agent is essentially rate limiting us, it's better to not retry for this scenario
                // We may come back to this in the future and determine a different/better strategy
                // https://github.com/DataDog/datadog-agent/pull/17917
                // StatusCode 429 -> Too Many Requests (agent getting too many traces and is overloaded)
                // StatusCode 413 -> Content Too Large (too large trace payload)
                // StatusCode 408 -> Request Timeout (agent timed out and closed connection - not the connection timing out and dropping)
                // Attempt a retry if the status code is not SUCCESS and NOT a status code that we shouldn't retry
                if ((response.StatusCode < 200 || response.StatusCode >= 300) && response.StatusCode != 429 && response.StatusCode != 413 && response.StatusCode != 408)
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

                    TelemetryFactory.Metrics.RecordCountTraceApiErrors(MetricTags.ApiError.StatusCode);
                    return finalTry ? SendResult.Failed_DontRetry : SendResult.Failed_CanRetry;
                }

                try
                {
                    if (_agentVersion == null)
                    {
                        var version = response.GetHeader(AgentHttpHeaderNames.AgentVersion);
                        LogPartialFlushWarningIfRequired(version ?? string.Empty);
                    }

                    if (response.ContentLength != 0 && _updateSampleRates is not null)
                    {
                        var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);

                        if (responseContent != _cachedResponse)
                        {
                            var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent);

                            _updateSampleRates(apiResponse.RateByService);

                            _cachedResponse = responseContent;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Traces sent successfully to the Agent at {AgentEndpoint}, but an error occurred deserializing the response.", _apiRequestFactory.Info(_tracesEndpoint));
                }

                if (response.StatusCode == 429 || response.StatusCode == 413 || response.StatusCode == 408)
                {
                    var retryAfter = response.GetHeader("Retry-After");
                    _log.Debug<int, string>("Failed to submit {Count} traces. Agent responded with 429 Too Many Requests, retry after {RetryAfter}", numberOfTraces, retryAfter ?? "unspecified");
                    return SendResult.Failed_DontRetry;
                }
                else
                {
                    _log.Debug<int>("Successfully sent {Count} traces to the Datadog Agent.", numberOfTraces);
                }
            }
            finally
            {
                response?.Dispose();
            }

            return SendResult.Success;
        }

        internal struct ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }

        private readonly struct SendTracesState
        {
            public readonly ArraySegment<byte> Traces;
            public readonly int NumberOfTraces;
            public readonly bool StatsComputationEnabled;
            public readonly long NumberOfDroppedP0Traces;
            public readonly long NumberOfDroppedP0Spans;
            public readonly bool AppsecStandaloneEnabled;

            public SendTracesState(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
            {
                Traces = traces;
                NumberOfTraces = numberOfTraces;
                StatsComputationEnabled = statsComputationEnabled;
                NumberOfDroppedP0Traces = numberOfDroppedP0Traces;
                NumberOfDroppedP0Spans = numberOfDroppedP0Spans;
                AppsecStandaloneEnabled = appsecStandaloneEnabled;
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
