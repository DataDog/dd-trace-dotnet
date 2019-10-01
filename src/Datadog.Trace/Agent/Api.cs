using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Containers;
using MsgPack.Serialization;
using Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.4/traces";

        private static readonly Vendoring.Serilog.ILogger Log = Vendoring.DatadogLogging.For<Api>();
        private static readonly SerializationContext SerializationContext = new SerializationContext();
        private static readonly SpanMessagePackSerializer Serializer = new SpanMessagePackSerializer(SerializationContext);

        private readonly Uri _tracesEndpoint;
        private readonly HttpClient _client;

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

        public Api(Uri baseEndpoint, DelegatingHandler delegatingHandler = null)
        {
            _client = delegatingHandler == null
                          ? new HttpClient()
                          : new HttpClient(delegatingHandler);

            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);

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
                Log.ErrorException("Error getting framework description", e);
            }

            // report Tracer version
            var tracerVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, tracerVersion);

            // report container id (only Linux containers supported for now)
            var containerId = ContainerInfo.GetContainerId();

            if (containerId != null)
            {
                _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.ContainerId, containerId);
            }

            // don't add automatic instrumentation to requests from this HttpClient
            _client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
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
                        responseMessage = await _client.PostAsync(_tracesEndpoint, content).ConfigureAwait(false);
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

                        Tracer.Instance.Sampler.SetSampleRates(response?.RateByService);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Traces sent successfully to the Agent at {Endpoint}, but an error occurred deserializing the response.", ex, _tracesEndpoint);
                }

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

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
