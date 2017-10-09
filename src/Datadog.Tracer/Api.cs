using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Datadog.Tracer
{
    internal class Api : IApi
    {
        private const string TracesPath = "/v0.3/traces";
        private const string ServicesPath = "/v0.3/services";

        private static MessagePackSerializer<List<List<Span>>> _traceSerializer;
        private static MessagePackSerializer<ServiceInfo> _serviceSerializer;

        static Api()
        {
            var serializationContext = new SerializationContext();
            var spanSerializer = new SpanMessagePackSerializer(serializationContext);
            var serviceSerializer = new ServiceInfoMessagePackSerializer(serializationContext);
            serializationContext.ResolveSerializer += (sender, eventArgs) => {
                if (eventArgs.TargetType == typeof(Span))
                {
                    eventArgs.SetSerializer(spanSerializer);
                }
                if (eventArgs.TargetType == typeof(ServiceInfo))
                {
                    eventArgs.SetSerializer(serviceSerializer);
                }
            };
            _traceSerializer = serializationContext.GetSerializer<List<List<Span>>>();
            _serviceSerializer = serializationContext.GetSerializer<ServiceInfo>();
        }

        private Uri _tracesEndpoint;
        private Uri _servicesEndpoint;
        private HttpClient _client = new HttpClient();

        public Api(Uri baseEndpoint)
        {
            _tracesEndpoint = new Uri(baseEndpoint, TracesPath);
            _servicesEndpoint = new Uri(baseEndpoint, ServicesPath);
        }

        public async Task SendTracesAsync(List<List<Span>> traces)
        {
            // TODO:bertrand avoid using a memory stream and stream the serialized content directly to the network
            using (var ms = new MemoryStream())
            {
                await _traceSerializer.PackAsync(ms, traces);
                var content = new ByteArrayContent(ms.GetBuffer());
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/msgpack");
                var response = await _client.PostAsync(_tracesEndpoint, content);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task SendServiceAsync(ServiceInfo service)
        {
            // TODO:bertrand avoid using a memory stream and stream the serialized content directly to the network
            using (var ms = new MemoryStream())
            {
                await _serviceSerializer.PackAsync(ms, service);
                var content = new ByteArrayContent(ms.GetBuffer());
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/msgpack");
                var response = await _client.PostAsync(_servicesEndpoint, content);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
