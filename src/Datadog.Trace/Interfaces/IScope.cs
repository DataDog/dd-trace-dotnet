using System;

namespace Datadog.Trace.Interfaces
{
    internal interface IScope : IDisposable
    {
        ISpan Span { get; }
    }
}
