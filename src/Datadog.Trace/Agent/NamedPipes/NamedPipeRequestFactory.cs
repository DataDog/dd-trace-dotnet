using System;

namespace Datadog.Trace.Agent
{
    internal class NamedPipeRequestFactory : IApiRequestFactory
    {
        public IApiRequest Create(Uri endpoint)
        {
            return new NamedPipesApiRequest("WEEE");
        }
    }
}
