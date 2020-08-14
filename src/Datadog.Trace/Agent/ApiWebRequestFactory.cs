using System;
using System.Net;

namespace Datadog.Trace.Agent
{
    internal class ApiWebRequestFactory : IApiRequestFactory
    {
        public IApiRequest Create(Uri endpoint)
        {
            return new ApiWebRequest(WebRequest.CreateHttp(endpoint));
        }
    }
}
