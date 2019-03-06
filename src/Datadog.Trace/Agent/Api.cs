using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using MsgPack.Serialization;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.3/traces";

        private static readonly ILog _log = LogProvider.For<Api>();
        private static readonly SerializationContext _serializationContext = new SerializationContext();

        private readonly Uri _tracesEndpoint;
        private readonly HttpClient _client;

        static Api()
        {
            var spanSerializer = new SpanMessagePackSerializer(_serializationContext);

            _serializationContext.ResolveSerializer += (sender, eventArgs) =>
            {
                if (eventArgs.TargetType == typeof(Span))
                {
                    eventArgs.SetSerializer(spanSerializer);
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
            await SendAsync(traces, _tracesEndpoint).ConfigureAwait(false);
        }

        private async Task SendAsync<T>(T value, Uri endpoint)
        {
            MsgPackContent<T> content;
            try
            {
                content = new MsgPackContent<T>(value, _serializationContext);
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
                    var response = await _client.PostAsync(endpoint, content).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (Exception ex)
                {
                    if (retryCount >= retryLimit)
                    {
                        _log.ErrorException("An error occurred while sending traces to the agent at {Endpoint}", ex, endpoint);
                        return;
                    }
                }

                await Task.Delay(sleepDuration).ConfigureAwait(false);

                retryCount++;
                sleepDuration *= 2;
            }
        }
    }
}
