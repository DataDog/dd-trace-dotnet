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

            foreach (var pair in AgentHttpHeaderNames.DefaultHeaders)
            {
                _client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
            }
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
