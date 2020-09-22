#if NETCOREAPP
using System;
using System.Net.Http;

namespace Datadog.Trace.Agent
{
    internal class HttpClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client;

        public HttpClientRequestFactory()
        {
            _client = new HttpClient();

            // Default headers
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.Language, ".NET");
            _client.DefaultRequestHeaders.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);

            // don't add automatic instrumentation to requests from this HttpClient
            _client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }
    }
}
#endif
