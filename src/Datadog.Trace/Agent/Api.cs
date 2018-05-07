using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using MsgPack.Serialization;

namespace Datadog.Trace.Agent
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.3/traces";

        private static ILog _log = LogProvider.For<Api>();
        private static SerializationContext _serializationContext;

        private Uri _tracesEndpoint;
        private HttpClient _client;

        static Api()
        {
            _serializationContext = new SerializationContext();
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
            if (delegatingHandler != null)
            {
                _client = new HttpClient(delegatingHandler);
            }
            else
            {
                _client = new HttpClient();
            }

            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);

            // TODO:bertrand add header for os version
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Lang", ".NET");
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Lang-Interpreter", RuntimeInformation.GetFrameworkVersion());
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Tracer-Version", this.GetType().Assembly.GetName().Version.ToString());
        }

        public async Task SendTracesAsync(IList<List<Span>> traces)
        {
            await SendAsync(traces, _tracesEndpoint);
        }

        private async Task SendAsync<T>(T value, Uri endpoint)
        {
            try
            {
                var content = new MsgPackContent<T>(value, _serializationContext);
                var response = await _client.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _log.ErrorException("An error occured while sending traces to the agent at {Endpoint}", ex, endpoint);
            }
        }
    }
}
