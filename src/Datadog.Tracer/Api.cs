using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
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
        private HttpClient _client = new HttpClient();

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
            _client.DefaultRequestHeaders.Add("Datadog-Meta-Tracer-Version", Assembly.GetEntryAssembly().GetName().Version.ToString());
        }

        public async Task SendTracesAsync(IList<List<Span>> traces)
        {
            try
            {
                var content = new MsgPackContent<IList<List<Span>>>(traces, _serializationContext);
                var response = await _client.PostAsync(_tracesEndpoint, content);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                //TODO:bertrand Log exception
            }
        }

        public async Task SendServiceAsync(ServiceInfo service)
        {
            try
            {
                var content = new MsgPackContent<ServiceInfo>(service, _serializationContext);
                var response = await _client.PostAsync(_servicesEndpoint, content);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // TODO:bertrand log exception 
            }
        }
    }
}
