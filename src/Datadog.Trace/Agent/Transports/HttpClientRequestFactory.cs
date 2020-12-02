#if NETCOREAPP
using System;
using System.Net.Http;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client;

        public HttpClientRequestFactory(HttpMessageHandler handler = null)
        {
            _client = handler == null ? new HttpClient() : new HttpClient(handler);

            // Default headers
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.Language, ".NET");
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.Version);

            // don't add automatic instrumentation to requests from this HttpClient
            _client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }
    }
}
#endif
