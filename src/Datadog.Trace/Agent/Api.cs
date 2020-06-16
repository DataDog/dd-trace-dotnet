using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using MessagePack;
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

        public async Task SendTracesAsync(Span[][] traces)
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

                IApiResponse response;
                try
                {
                    try
                    {
                        _statsd?.AppendIncrementCount(TracerMetricNames.Api.Requests);
                        response = await request.PostAsync(traces, _formatterResolver).ConfigureAwait(false);
                    }
                    catch
                    {
                        // count the exceptions thrown by the HttpClient,
                        // not responses with 5xx status codes
                        // (which cause EnsureSuccessStatusCode() to throw below)
                        _statsd?.AppendIncrementCount(TracerMetricNames.Api.Errors);
                        throw;
                    }

                    if (_statsd != null)
                    {
                        // don't bother creating the tags array if trace metrics are disabled
                        // TODO: REMOVE // string[] tags = { $"status:{(int)responseMessage.StatusCode}" };
                        string[] tags = { $"status:{response.StatusCode}" };

                        // count every response, grouped by status code
                        _statsd.AppendIncrementCount(TracerMetricNames.Api.Responses, tags: tags);
                    }

                    // Attempt a retry if the status code is not SUCCESS
                    if (response.StatusCode < 200 || response.StatusCode > 300)
                    {
                        if (retryCount >= retryLimit)
                        {
                            // stop retrying
                            Log.Error("An error occurred while sending traces to the agent at {Endpoint}", _tracesEndpoint);
                            return;
                        }

                        // retry
                        await Task.Delay(sleepDuration).ConfigureAwait(false);
                        retryCount++;
                        sleepDuration *= 2;

                        continue;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error("An error occurred while sending traces to the agent at {Endpoint}\n{Exception}", ex, _tracesEndpoint, ex.ToString());
                        return;
                    }
#endif
                    var isSocketException = false;
                    if (ex.InnerException is SocketException se)
                    {
                        isSocketException = true;
                        Log.Error(se, "Unable to communicate with the trace agent at {Endpoint}", _tracesEndpoint);
                        TracingProcessManager.TryForceTraceAgentRefresh();
                    }

                    if (retryCount >= retryLimit)
                    {
                        // stop retrying
                        Log.Error("An error occurred while sending traces to the agent at {Endpoint}", ex, _tracesEndpoint);
                        return;
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
                    Log.Error("Traces sent successfully to the Agent at {Endpoint}, but an error occurred deserializing the response.", ex, _tracesEndpoint);
                }

                _statsd?.Send();
                return;
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

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
