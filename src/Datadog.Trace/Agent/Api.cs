using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
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

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Api>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDogStatsd _statsd;
        private readonly FormatterResolverWrapper _formatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);
        private readonly string _containerId;
        private Uri _tracesEndpoint; // The Uri may be reassigned dynamically so that retry attempts may attempt updated Agent ports
        private string _cachedResponse;

        public Api(Uri baseEndpoint, IApiRequestFactory apiRequestFactory, IDogStatsd statsd)
        {
            Log.Debug("Creating new Api");

            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
            _statsd = statsd;
            _containerId = ContainerMetadata.GetContainerId();
            _apiRequestFactory = apiRequestFactory ?? CreateRequestFactory();

            // report runtime details
            Log.Information(FrameworkDescription.Instance.ToString());
        }

        public void SetBaseEndpoint(Uri baseEndpoint)
        {
            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
        }

        public async Task<bool> SendTracesAsync(Span[][] traces)
        {
            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            Log.Debug("Sending {0} traces to the DD agent", traces.Length);

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(_tracesEndpoint);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"An error occurred while generating http request to send traces to the agent at {_tracesEndpoint}");
                    return false;
                }

                // Set additional headers
                request.AddHeader(AgentHttpHeaderNames.TraceCount, traces.Length.ToString());
                request.AddHeader(AgentHttpHeaderNames.LanguageInterpreter, FrameworkDescription.Instance.Name);
                request.AddHeader(AgentHttpHeaderNames.LanguageVersion, FrameworkDescription.Instance.ProductVersion);

                if (_containerId != null)
                {
                    request.AddHeader(AgentHttpHeaderNames.ContainerId, _containerId);
                }

                bool success = false;
                Exception exception = null;
                bool isFinalTry = retryCount >= retryLimit;

                try
                {
                    success = await SendTracesAsync(traces, request, isFinalTry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error(ex, "An error occurred while sending {0} traces to the agent at {1}", traces.Length, _tracesEndpoint);
                        return false;
                    }
#endif
                    exception = ex;
                }

                // Error handling block
                if (!success)
                {
                    if (isFinalTry)
                    {
                        // stop retrying
                        Log.Error(exception, "An error occurred while sending {0} traces to the agent at {1}", traces.Length, _tracesEndpoint);
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
                        Log.Debug(exception, "Unable to communicate with the trace agent at {0}", _tracesEndpoint);
                        TracingProcessManager.TryForceTraceAgentRefresh();
                    }

                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    // After retry delay
                    if (isSocketException)
                    {
                        // Ensure we have the most recent port before trying again
                        TracingProcessManager.TraceAgentMetadata.ForcePortFileRead();
                    }

                    continue;
                }

                Log.Debug("Successfully sent {0} traces to the DD agent", traces.Length);
                return true;
            }
        }

        private static IApiRequestFactory CreateRequestFactory()
        {
#if NETCOREAPP
            return new HttpClientRequestFactory();
#else
            return new ApiWebRequestFactory();
#endif
        }

        private async Task<bool> SendTracesAsync(Span[][] traces, IApiRequest request, bool finalTry)
        {
            IApiResponse response = null;

            try
            {
                try
                {
                    _statsd?.Increment(TracerMetricNames.Api.Requests);
                    response = await request.PostAsync(traces, _formatterResolver).ConfigureAwait(false);
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
                            Log.Error("Failed to submit traces with status code {0} and message: {1}", response.StatusCode, responseContent);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Unable to read response for failed request with status code {0}", response.StatusCode);
                        }
                    }

                    return false;
                }

                try
                {
                    if (response.ContentLength != 0 && Tracer.Instance.Sampler != null)
                    {
                        var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);

                        if (responseContent != _cachedResponse)
                        {
                            var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent);

                            Tracer.Instance.Sampler.SetDefaultSampleRates(apiResponse?.RateByService);

                            _cachedResponse = responseContent;
                        }
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
