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

        private static MessagePackSerializer<IList<List<Span>>> _traceSerializer;
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
            _traceSerializer = serializationContext.GetSerializer<IList<List<Span>>>();
            _serviceSerializer = serializationContext.GetSerializer<ServiceInfo>();
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
            catch
            {
                //TODO:bertrand Log exception
            }
        }

        public async Task SendServiceAsync(ServiceInfo service)
        {
            try
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
            catch
            {
                // TODO:bertrand log exception 
            }
        }
    }
}
