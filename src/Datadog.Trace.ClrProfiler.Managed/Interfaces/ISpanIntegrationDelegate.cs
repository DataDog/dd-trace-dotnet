using System;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface ISpanIntegrationDelegate : IDisposable
    {
        IScope Scope { get; }

        void OnBegin();

        void OnEnd();

        void OnError();
    }
}
