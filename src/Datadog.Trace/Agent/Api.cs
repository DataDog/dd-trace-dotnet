using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Containers;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;
using MsgPack.Serialization;
using Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.4/traces";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Api>();
        private static readonly SerializationContext SerializationContext = new SerializationContext();
        private static readonly SpanMessagePackSerializer Serializer = new SpanMessagePackSerializer(SerializationContext);

        private static readonly object EndpointGate = new object();
        private static int? _tracePortOverride;
        private static Uri _baseEndpoint;
        private static Uri _tracesEndpoint;

        private readonly HttpClient _client;
        private readonly IStatsd _statsd;

        static Api()
        {
            SerializationContext.ResolveSerializer += (sender, eventArgs) =>
            {
                if (eventArgs.TargetType == typeof(Span))
                {
                    eventArgs.SetSerializer(Serializer);
                }
            };
        }

        public Api(Uri baseEndpoint, DelegatingHandler delegatingHandler, IStatsd statsd)
        {
            lock (EndpointGate)
            {
                _baseEndpoint = baseEndpoint;
                if (_tracePortOverride != null)
                {
                    SetTracesEndpointUri(_tracePortOverride.Value);
                }
                else
                {
                    _tracesEndpoint = new Uri(_baseEndpoint, TracesPath);
                }
            }

            _statsd = statsd;

            _client = delegatingHandler == null
                          ? new HttpClient()
                          : new HttpClient(delegatingHandler);

            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.Language, ".NET");

            // report runtime details
            try
            {
                var frameworkDescription = FrameworkDescription.Create();

                if (frameworkDescription != null)
                {
                    _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageInterpreter, frameworkDescription.Name);
                    _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageVersion, frameworkDescription.ProductVersion);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error getting framework description");
            }

            // report Tracer version
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);

            // report container id (only Linux containers supported for now)
            var containerId = ContainerInfo.GetContainerId();

            if (containerId != null)
            {
                _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.ContainerId, containerId);
            }

            // don't add automatic instrumentation to requests from this HttpClient
            _client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public static void OverrideTracePort(int port)
        {
            lock (EndpointGate)
            {
                _tracePortOverride = port;

                if (_baseEndpoint != null)
                {
                    SetTracesEndpointUri(port);
                }
            }
        }

        public async Task SendTracesAsync(IList<List<Span>> traces)
        {
            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            while (true)
            {
                HttpResponseMessage responseMessage;

                try
                {
                    var traceIds = GetUniqueTraceIds(traces);

                    // re-create content on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
                    using (var content = new MsgPackContent<IList<List<Span>>>(traces, SerializationContext))
                    {
                        content.Headers.Add(AgentHttpHeaderNames.TraceCount, traceIds.Count.ToString());

                        try
                        {
                            _statsd?.AppendIncrementCount(TracerMetricNames.Api.Requests);
                            responseMessage = await _client.PostAsync(_tracesEndpoint, content).ConfigureAwait(false);
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
                            string[] tags = { $"status:{(int)responseMessage.StatusCode}" };

                            // count every response, grouped by status code
                            _statsd.AppendIncrementCount(TracerMetricNames.Api.Responses, tags: tags);
                        }

                        responseMessage.EnsureSuccessStatusCode();
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
                    continue;
                }

                try
                {
                    if (responseMessage.Content != null && Tracer.Instance.Sampler != null)
                    {
                        // build the sample rate map from the response json
                        var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var response = JsonConvert.DeserializeObject<ApiResponse>(responseContent);

                        Tracer.Instance.Sampler.SetDefaultSampleRates(response?.RateByService);
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

        private static HashSet<ulong> GetUniqueTraceIds(IList<List<Span>> traces)
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

        private static void SetTracesEndpointUri(int port)
        {
            var builder = new UriBuilder(_baseEndpoint) { Port = port };
            var newUri = builder.Uri;
            _tracesEndpoint = new Uri(newUri, TracesPath);
        }

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
