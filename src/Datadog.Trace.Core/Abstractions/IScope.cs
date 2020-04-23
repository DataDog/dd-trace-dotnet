using System;

namespace Datadog.Trace.Abstractions
{
    internal interface IScope : IDisposable
    {
        ISpan Span { get; }
    }
}
