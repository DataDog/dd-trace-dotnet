using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.4/traces";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Api>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IStatsd _statsd;
        private readonly Uri _tracesEndpoint;
        private readonly FormatterResolverWrapper _formatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);
        private readonly string _containerId;
        private readonly FrameworkDescription _frameworkDescription;

        public Api(Uri baseEndpoint, IApiRequestFactory apiRequestFactory, IStatsd statsd)
        {
            Log.Debug("Creating new Api");

            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
            _statsd = statsd;
            _containerId = ContainerMetadata.GetContainerId();
            _apiRequestFactory = apiRequestFactory ?? new ApiWebRequestFactory();

            // report runtime details
            try
            {
                _frameworkDescription = FrameworkDescription.Create();

                if (_frameworkDescription != null)
                {
                    Log.Information(_frameworkDescription.ToString());
                }
            }
            catch (Exception e)
            {
                Log.SafeLogError(e, "Error getting framework description");
            }
        }

        public async Task<bool> SendTracesAsync(Span[][] traces)
        {
            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds
            var traceIds = GetUniqueTraceIds(traces);

            while (true)
            {
                var request = _apiRequestFactory.Create(_tracesEndpoint);

                // Set additional headers
                request.AddHeader(AgentHttpHeaderNames.TraceCount, traceIds.Count.ToString());
                if (_frameworkDescription != null)
                {
                    request.AddHeader(AgentHttpHeaderNames.LanguageInterpreter, _frameworkDescription.Name);
                    request.AddHeader(AgentHttpHeaderNames.LanguageVersion, _frameworkDescription.ProductVersion);
                }

                if (_containerId != null)
                {
                    request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
                }

                bool success = false;
                Exception exception = null;

                try
                {
                    success = await SendTracesAsync(traces, request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error(ex, "An error occurred while sending traces to the agent at {0}", _tracesEndpoint);
                        return false;
                    }
#endif
                    exception = ex;
                }

                // Error handling block
                if (!success)
                {
                    bool isSocketException = false;

                    if (exception?.InnerException is SocketException se)
                    {
                        isSocketException = true;
                        Log.Error(se, "Unable to communicate with the trace agent at {0}", _tracesEndpoint);
                        TracingProcessManager.TryForceTraceAgentRefresh();
                    }

                    if (retryCount >= retryLimit)
                    {
                        // stop retrying
                        Log.Error(exception, "An error occurred while sending traces to the agent at {0}", _tracesEndpoint);
                        return false;
                    }

                    // retry
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    if (isSocketException)
                    {
                        // Ensure we have the most recent port before trying again
                        TracingProcessManager.TraceAgentMetadata.ForcePortFileRead();
                    }

                    continue;
                }

                _statsd?.Send();
                return true;
            }
        }

        private static HashSet<ulong> GetUniqueTraceIds(Span[][] traces)
        {
            var uniqueTraceIds = new HashSet<ulong>();

            foreach (var trace in traces)
            {
                foreach (var span in trace)
                {
                    uniqueTraceIds.Add(span.TraceId);
                }
            }

            return uniqueTraceIds;
        }

        private async Task<bool> SendTracesAsync(Span[][] traces, IApiRequest request)
        {
            IApiResponse response = null;

            try
            {
                try
                {
                    _statsd?.AppendIncrementCount(TracerMetricNames.Api.Requests);
                    response = await request.PostAsync(traces, _formatterResolver).ConfigureAwait(false);
                }
                catch
                {
                    // count only network/infrastructure errors, not valid responses with error status codes
                    // (which are handled below)
                    _statsd?.AppendIncrementCount(TracerMetricNames.Api.Errors);
                    throw;
                }

                if (_statsd != null)
                {
                    // don't bother creating the tags array if trace metrics are disabled
                    string[] tags = { $"status:{response.StatusCode}" };

                    // count every response, grouped by status code
                    _statsd.AppendIncrementCount(TracerMetricNames.Api.Responses, tags: tags);
                }

                // Attempt a retry if the status code is not SUCCESS
                if (response.StatusCode < 200 || response.StatusCode >= 300)
                {
                    return false;
                }

                try
                {
                    if (response.ContentLength > 0 && Tracer.Instance.Sampler != null)
                    {
                        var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                        var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent);
                        Tracer.Instance.Sampler.SetDefaultSampleRates(apiResponse?.RateByService);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Traces sent successfully to the Agent at {0}, but an error occurred deserializing the response.", _tracesEndpoint);
                }
            }
            finally
            {
                response?.Dispose();
            }

            return true;
        }

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
