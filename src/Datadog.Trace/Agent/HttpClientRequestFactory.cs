#if NETCOREAPP
using System;
using System.Net.Http;

namespace Datadog.Trace.Agent
{
    internal class HttpClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client = new HttpClient();

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }
    }
}
#endif
