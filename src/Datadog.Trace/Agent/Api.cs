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

            // TODO:bertrand add header for os version
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.Language, ".NET");
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.LanguageInterpreter, RuntimeInformation.FrameworkDescription);
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, this.GetType().Assembly.GetName().Version.ToString());

            // don't add automatic instrumentation to requests from this HttpClient
            _client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public async Task SendTracesAsync(IList<List<Span>> traces)
        {
            MsgPackContent<IList<List<Span>>> content;

            try
            {
                content = new MsgPackContent<IList<List<Span>>>(traces, _serializationContext);
            }
            catch (Exception ex)
            {
                _log.ErrorException("An error occurred while serializing traces", ex);
                return;
            }

            // retry up to 5 times with exponential backoff
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            while (true)
            {
                try
                {
                    var responseMessage = await _client.PostAsync(_tracesEndpoint, content).ConfigureAwait(false);
                    responseMessage.EnsureSuccessStatusCode();

                    // build the sample rate map from the response json
                    var responseContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var response = JsonConvert.DeserializeObject<ApiResponse>(responseContent);

                    Tracer.Instance.Sampler?.SetSampleRates(response.RateByService);
                    return;
                }
                catch (Exception ex)
                {
                    if (retryCount >= retryLimit)
                    {
                        _log.ErrorException("An error occurred while sending traces to the agent at {Endpoint}", ex, _tracesEndpoint);
                        return;
                    }
                }

                await Task.Delay(sleepDuration).ConfigureAwait(false);

                retryCount++;
                sleepDuration *= 2;
            }
        }

        internal class ApiResponse
        {
            [JsonProperty("rate_by_service")]
            public Dictionary<string, float> RateByService { get; set; }
        }
    }
}
