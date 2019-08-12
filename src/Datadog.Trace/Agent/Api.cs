using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using MsgPack.Serialization;
using Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.4/traces";

        private static readonly ILog Log = LogProvider.For<Api>();
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

            GetFrameworkDescription(out string frameworkName, out string frameworkVersion);
            var tracerVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.Language, ".NET");
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageInterpreter, frameworkName);
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageVersion, frameworkVersion);
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, tracerVersion);

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
                        Log.ErrorException("An error occurred while sending traces to the agent at {Endpoint}\n{Exception}", ex, _tracesEndpoint, ex.ToString());
                        return;
                    }
#endif
                    if (retryCount >= retryLimit)
                    {
                        // stop retrying
                        Log.ErrorException("An error occurred while sending traces to the agent at {Endpoint}", ex, _tracesEndpoint);
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
                    Log.ErrorException("Traces sent successfully to the Agent at {Endpoint}, but an error occurred deserializing the response.", ex, _tracesEndpoint);
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

        private static void GetFrameworkDescription(out string name, out string version)
        {
            // RuntimeInformation.FrameworkDescription returns string like ".NET Framework 4.7.2" or ".NET Core 2.1",
            // we want to split the runtime from the version so we can report them as separate values
            string frameworkDescription = RuntimeInformation.FrameworkDescription;
            int index = RuntimeInformation.FrameworkDescription.LastIndexOf(' ');

            // everything before the last space
            name = frameworkDescription.Substring(0, index).Trim();

            // everything after the last space
            version = frameworkDescription.Substring(index).Trim();
        }

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
