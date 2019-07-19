using System;
using System.Collections.Generic;
using System.Net.Http;
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

        private static readonly ILog _log = LogProvider.For<Api>();
        private static readonly SerializationContext _serializationContext = new SerializationContext();
        private static readonly SpanMessagePackSerializer Serializer = new SpanMessagePackSerializer(_serializationContext);

        private readonly Uri _tracesEndpoint;
        private readonly HttpClient _client;

        static Api()
        {
            _serializationContext.ResolveSerializer += (sender, eventArgs) =>
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

            var interpreterVersion = GetInterpreterVersion();
            var managedAssemblyVersion = this.GetType().Assembly.GetName().Version.ToString();

            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.Language, ".NET");
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageInterpreter, interpreterVersion.Item1);
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageVersion, interpreterVersion.Item2);
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, managedAssemblyVersion);

            // don't add automatic instrumentation to requests from this HttpClient
            _client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public async Task SendTracesAsync(IList<List<Span>> traces)
        {
            // retry up to 5 times with exponential backoff
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            while (true)
            {
                HttpResponseMessage responseMessage;

                try
                {
                    // re-create content on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
                    using (var content = new MsgPackContent<IList<List<Span>>>(traces, _serializationContext))
                    {
                        responseMessage = await _client.PostAsync(_tracesEndpoint, content).ConfigureAwait(false);
                        responseMessage.EnsureSuccessStatusCode();
                    }
                }
                catch (Exception ex)
                {
                    if (retryCount >= retryLimit)
                    {
                        // stop retrying
                        _log.ErrorException("An error occurred while sending traces to the agent at {Endpoint}", ex, _tracesEndpoint);
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
                    _log.ErrorException("Traces sent successfully to the Agent at {Endpoint}, but an error occurred deserializing the response.", ex, _tracesEndpoint);
                }

                return;
            }
        }

        private static Tuple<string, string> GetInterpreterVersion()
        {
            // RuntimeInformation.FrameworkDescription returns string like ".NET Framework 4.7.2" or ".NET Core 2.1",
            // we want to split the runtime from the version so we can report them as separate values
            string frameworkDescription = RuntimeInformation.FrameworkDescription;
            int index = RuntimeInformation.FrameworkDescription.LastIndexOf(' ');

            // everything before the last space
            string interpreter = frameworkDescription.Substring(0, index).Trim();

            // everything after the last space
            string version = frameworkDescription.Substring(index).Trim();
            return Tuple.Create(interpreter, version);
        }

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
