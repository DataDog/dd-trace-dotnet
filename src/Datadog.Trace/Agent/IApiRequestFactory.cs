using System;

namespace Datadog.Trace.Agent
{
    internal interface IApiRequestFactory
    {
        string Info(Uri endpoint);

        IApiRequest Create(Uri endpoint);
    }
}
