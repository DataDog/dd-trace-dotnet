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
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.4/traces";
        private const string StatsPath = "/v0.6/stats";

        private static readonly IDatadogLogger StaticLog = DatadogLogging.GetLoggerFor<Api>();

        private readonly IDatadogLogger _log;
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDogStatsd _statsd;
        private readonly string _containerId;
        private readonly Uri _tracesEndpoint;
        private readonly Uri _statsEndpoint;
        private readonly Action<Dictionary<string, float>> _updateSampleRates;
        private readonly bool _partialFlushEnabled;
        private readonly bool _statsComputationEnabled;
        private readonly SendCallback<SendStatsState> _sendStats;
        private readonly SendCallback<SendTracesState> _sendTraces;
        private string _cachedResponse;
        private string _agentVersion;

        public Api(
            IApiRequestFactory apiRequestFactory,
            IDogStatsd statsd,
            Action<Dictionary<string, float>> updateSampleRates,
            bool partialFlushEnabled,
            bool statsComputationEnabled,
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
            _apiRequestFactory = apiRequestFactory;
            _partialFlushEnabled = partialFlushEnabled;
            _tracesEndpoint = _apiRequestFactory.GetEndpoint(TracesPath);
            _log.Debug("Using traces endpoint {TracesEndpoint}", _tracesEndpoint.ToString());
            _statsEndpoint = _apiRequestFactory.GetEndpoint(StatsPath);
            _log.Debug("Using stats endpoint {StatsEndpoint}", _statsEndpoint.ToString());
            _statsComputationEnabled = statsComputationEnabled;
        }

        private delegate Task<bool> SendCallback<T>(IApiRequest request, bool isFinalTry, T state);

        public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        {
            _log.Debug("Sending stats to the Datadog Agent.");

            var state = new SendStatsState(stats, bucketDuration);

            return SendWithRetry(_statsEndpoint, _sendStats, state);
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces)
        {
            _log.Debug<int>("Sending {Count} traces to the Datadog Agent.", numberOfTraces);

            var state = new SendTracesState(traces, numberOfTraces);

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

                        _log.Warning("DATADOG TRACER DIAGNOSTICS - Partial flush should only be enabled with agent 7.26.0+ (detected version: {version})", detectedVersion);
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

                bool success = false;
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
                if (!success)
                {
                    if (isFinalTry)
                    {
                        // stop retrying
                        _log.Error(exception, "An error occurred while sending data to the agent at {AgentEndpoint}", _apiRequestFactory.Info(endpoint));
                        return false;
                    }

                    // Before retry delay
                    bool isSocketException = false;
                    Exception innerException = exception;

                    while (innerException != null)
                    {
                        if (innerException is SocketException)
                        {
                            isSocketException = true;
                            break;
                        }

                        innerException = innerException.InnerException;
                    }

                    if (isSocketException)
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

        private async Task<bool> SendStatsAsyncImpl(IApiRequest request, bool isFinalTry, SendStatsState state)
        {
            bool success = false;

            // Set additional headers
            if (_containerId != null)
            {
                request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
            }

            using var stream = new MemoryStream();
            state.Stats.Serialize(stream, state.BucketDuration);

            var buffer = stream.GetBuffer();

            using var response = await request.PostAsync(new ArraySegment<byte>(buffer, 0, (int)stream.Length), MimeTypes.MsgPack).ConfigureAwait(false);

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

            return success;
        }

        private async Task<bool> SendTracesAsyncImpl(IApiRequest request, bool finalTry, SendTracesState state)
        {
            IApiResponse response = null;

            var traces = state.Traces;
            var numberOfTraces = state.NumberOfTraces;

            // Set additional headers
            request.AddHeader(AgentHttpHeaderNames.TraceCount, numberOfTraces.ToString());

            if (_containerId != null)
            {
                request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
            }

            if (_statsComputationEnabled)
            {
                request.AddHeader(AgentHttpHeaderNames.StatsComputation, "true");
            }

            try
            {
                try
                {
                    _statsd?.Increment(TracerMetricNames.Api.Requests);
                    response = await request.PostAsync(traces, MimeTypes.MsgPack).ConfigureAwait(false);
                }
                catch
                {
                    // count only network/infrastructure errors, not valid responses with error status codes
                    // (which are handled below)
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

                // Attempt a retry if the status code is not SUCCESS
                if (response.StatusCode < 200 || response.StatusCode >= 300)
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

                    return false;
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
            }
            finally
            {
                response?.Dispose();
            }

            _log.Debug<int>("Successfully sent {Count} traces to the Datadog Agent.", numberOfTraces);

            return true;
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

            public SendTracesState(ArraySegment<byte> traces, int numberOfTraces)
            {
                Traces = traces;
                NumberOfTraces = numberOfTraces;
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
