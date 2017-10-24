using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Datadog.Tracer
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.3/traces";
        private const string ServicesPath = "/v0.3/services";
        private static SerializationContext _serializationContext;

        static Api()
        {
            _serializationContext = new SerializationContext();
            var spanSerializer = new SpanMessagePackSerializer(_serializationContext);
            var serviceSerializer = new ServiceInfoMessagePackSerializer(_serializationContext);
            _serializationContext.ResolveSerializer += (sender, eventArgs) => {
                if (eventArgs.TargetType == typeof(Span))
                {
                    eventArgs.SetSerializer(spanSerializer);
                }
                if (eventArgs.TargetType == typeof(ServiceInfo))
                {
                    eventArgs.SetSerializer(serviceSerializer);
                }
            };
        }

        private Uri _tracesEndpoint;
        private Uri _servicesEndpoint;
        private HttpClient _client;

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
            _servicesEndpoint = new Uri(baseEndpoint, ServicesPath);
            // TODO:bertrand add header for os version
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Lang", ".NET");
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Lang-Interpreter", RuntimeInformation.FrameworkDescription);
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Tracer-Version", Assembly.GetAssembly(typeof(Api)).GetName().Version.ToString());
        }

        private async Task SendAsync<T>(T value, Uri endpoint)
        {
            try
            {
                var content = new MsgPackContent<T>(value, _serializationContext);
                var response = await _client.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // TODO:bertrand log
            }
        }

        public async Task SendTracesAsync(IList<List<Span>> traces)
        {
            await SendAsync(traces, _tracesEndpoint);
        }

        public async Task SendServiceAsync(ServiceInfo service)
        {
            await SendAsync(service, _servicesEndpoint);
        }
    }
}
