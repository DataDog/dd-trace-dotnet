using System;
using Datadog.Trace.Agent.NamedPipes;

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
