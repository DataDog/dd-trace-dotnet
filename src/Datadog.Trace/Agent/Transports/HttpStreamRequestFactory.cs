using System;
using System.IO;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamRequestFactory : IApiRequestFactory
    {
        private readonly IStreamFactory _streamFactory;

        public HttpStreamRequestFactory(IStreamFactory streamFactory)
        {
            _streamFactory = streamFactory;
        }

        public IApiRequest Create(Uri endpoint)
        {
            _streamFactory.GetStreams(out Stream requestStream, out Stream responseStream);
            return new HttpStreamRequest(endpoint, requestStream, responseStream);
        }
    }
}
