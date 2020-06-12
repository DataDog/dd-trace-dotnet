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

        private readonly IStatsd _statsd;
        private readonly Uri _tracesEndpoint;
        private readonly FormatterResolverWrapper _formatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);
        private readonly string _containerId;
        private readonly FrameworkDescription _frameworkDescription;

        public Api(Uri baseEndpoint, IStatsd statsd)
        {
            Log.Debug("Creating new Api");

            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
            _statsd = statsd;
            _containerId = ContainerMetadata.GetContainerId();

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
                var request = (HttpWebRequest)WebRequest.Create(_tracesEndpoint);
                request.ContentType = "application/msgpack";
                request.Method = "POST";

                // Default headers
                request.Headers.Add(AgentHttpHeaderNames.Language, ".NET");
                request.Headers.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);

                if (_frameworkDescription != null)
                {
                    request.Headers.Add(AgentHttpHeaderNames.LanguageInterpreter, _frameworkDescription.Name);
                    request.Headers.Add(AgentHttpHeaderNames.LanguageVersion, _frameworkDescription.ProductVersion);
                }

                if (_containerId != null)
                {
                    request.Headers.Add(AgentHttpHeaderNames.ContainerId, _containerId);
                }

                // don't add automatic instrumentation to requests from this HttpClient
                request.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
                request.Headers.Add(AgentHttpHeaderNames.TraceCount, traceIds.Count.ToString());

                HttpWebResponse response;
                try
                {
                    using (var requestStream = await request.GetRequestStreamAsync())
                    {
#if MESSAGEPACK_1_9
                        await MessagePackSerializer.SerializeAsync(requestStream, traces, _formatterResolver);
#elif MESSAGEPACK_2_1
                        await MessagePackSerializer.SerializeAsync(requestStream, traces, _formatterResolver.Options);
#endif
                    }

                    try
                    {
                        _statsd?.AppendIncrementCount(TracerMetricNames.Api.Requests);
                        response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
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
                        string[] tags = { $"status:{(int)response.StatusCode}" };

                        // count every response, grouped by status code
                        _statsd.AppendIncrementCount(TracerMetricNames.Api.Responses, tags: tags);
                    }

                    // Attempt a retry if the status code is not SUCCESS
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode > 300)
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
                        // build the sample rate map from the response json
                        using (var responseStream = response.GetResponseStream())
                        {
                            var reader = new StreamReader(responseStream);
                            var responseContent = await reader.ReadToEndAsync();
                            var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent);
                            Tracer.Instance.Sampler.SetDefaultSampleRates(apiResponse?.RateByService);
                        }
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
