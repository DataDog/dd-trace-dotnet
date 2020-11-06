using System;

namespace Datadog.Trace.Agent
{
    internal interface IApiRequestFactory
    {
        IApiRequest Create(Uri endpoint);
    }
}
