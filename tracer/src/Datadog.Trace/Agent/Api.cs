// <copyright file="Api.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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

        private static readonly IDatadogLogger StaticLog = DatadogLogging.GetLoggerFor<Api>();

        private readonly IDatadogLogger _log;
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDogStatsd _statsd;
        private readonly string _containerId;
        private readonly Uri _tracesEndpoint;
        private readonly Action<Dictionary<string, float>> _updateSampleRates;
        private readonly bool _isPartialFlushEnabled;
        private string _cachedResponse;
        private string _agentVersion;

        public Api(
            Uri baseEndpoint,
            IApiRequestFactory apiRequestFactory,
            IDogStatsd statsd,
            Action<Dictionary<string, float>> updateSampleRates,
            bool isPartialFlushEnabled,
            IDatadogLogger log = null)
        {
            // optionally injecting a log instance in here for testing purposes
            _log = log ?? StaticLog;
            _log.Debug("Creating new Api");
            _updateSampleRates = updateSampleRates;
            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
            _statsd = statsd;
            _containerId = ContainerMetadata.GetContainerId();
            _apiRequestFactory = apiRequestFactory ?? CreateRequestFactory();
            _isPartialFlushEnabled = isPartialFlushEnabled;
        }

        public async Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces)
        {
            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            _log.Debug<int>("Sending {Count} traces to the DD agent", numberOfTraces);

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(_tracesEndpoint);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "An error occurred while generating http request to send traces to the agent at {AgentEndpoint}", _apiRequestFactory.Info(_tracesEndpoint));
                    return false;
                }

                // Set additional headers
                request.AddHeader(AgentHttpHeaderNames.TraceCount, numberOfTraces.ToString());

                if (_containerId != null)
                {
                    request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
                }

                bool success = false;
                Exception exception = null;
                bool isFinalTry = retryCount >= retryLimit;

                try
                {
                    success = await SendTracesAsync(traces, numberOfTraces, request, isFinalTry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exception = ex;
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        _log.Error<int, string>(ex, "An error occurred while sending {Count} traces to the agent at {AgentEndpoint}", numberOfTraces, _apiRequestFactory.Info(_tracesEndpoint));
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
                        _log.Error<int, string>(exception, "An error occurred while sending {Count} traces to the agent at {AgentEndpoint}", numberOfTraces, _apiRequestFactory.Info(_tracesEndpoint));
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
                        _log.Debug(exception, "Unable to communicate with the trace agent at {AgentEndpoint}", _apiRequestFactory.Info(_tracesEndpoint));
                    }

                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    continue;
                }

                _log.Debug<int>("Successfully sent {Count} traces to the DD agent", numberOfTraces);
                return true;
            }
        }

        internal static IApiRequestFactory CreateRequestFactory()
        {
#if NETCOREAPP
            StaticLog.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            return new HttpClientRequestFactory();
#else
            StaticLog.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
            return new ApiWebRequestFactory();
#endif
        }

        private async Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, IApiRequest request, bool finalTry)
        {
            IApiResponse response = null;

            try
            {
                try
                {
                    _statsd?.Increment(TracerMetricNames.Api.Requests);
                    response = await request.PostAsync(traces, "application/msgpack").ConfigureAwait(false);
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

            return true;
        }

        // internal for testing
        internal bool LogPartialFlushWarningIfRequired(string agentVersion)
        {
            if (agentVersion != _agentVersion)
            {
                _agentVersion = agentVersion;

                if (_isPartialFlushEnabled)
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

        internal struct ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
