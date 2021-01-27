using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
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

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Api>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDogStatsd _statsd;
        private readonly FormatterResolverWrapper _formatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);
        private readonly string _containerId;
        private readonly Uri _tracesEndpoint;
        private string _cachedResponse;

        public Api(Uri baseEndpoint, IApiRequestFactory apiRequestFactory, IDogStatsd statsd)
        {
            Log.Debug("Creating new Api");
            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
            _statsd = statsd;
            _containerId = ContainerMetadata.GetContainerId();
            _apiRequestFactory = apiRequestFactory ?? CreateRequestFactory();
        }

        public async Task<bool> SendTracesAsync(Span[][] traces)
        {
            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            Log.Debug("Sending {Count} traces to the DD agent", traces.Length);

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(_tracesEndpoint);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while generating http request to send traces to the agent at {AgentEndpoint}", _apiRequestFactory.Info(_tracesEndpoint));
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
                    exception = ex;
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error(ex, "An error occurred while sending {Count} traces to the agent at {AgentEndpoint}", traces.Length, _apiRequestFactory.Info(_tracesEndpoint));
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
                        Log.Error(exception, "An error occurred while sending {Count} traces to the agent at {AgentEndpoint}", traces.Length, _apiRequestFactory.Info(_tracesEndpoint));
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
                        // Somewhat expected, so just warn instead of error
                        Log.Warning(exception, "Unable to communicate with the trace agent at {AgentEndpoint}", _apiRequestFactory.Info(_tracesEndpoint));
                    }

                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    continue;
                }

                Log.Debug("Successfully sent {Count} traces to the DD agent", traces.Length);
                return true;
            }
        }

        private static IApiRequestFactory CreateRequestFactory()
        {
#if NETCOREAPP
            Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            return new HttpClientRequestFactory();
#else
            Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
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
                            Log.Error("Failed to submit traces with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
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
                    Log.Error(ex, "Traces sent successfully to the Agent at {AgentEndpoint}, but an error occurred deserializing the response.", _apiRequestFactory.Info(_tracesEndpoint));
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
