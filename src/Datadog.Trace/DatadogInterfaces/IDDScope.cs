using System;

namespace Datadog.Trace
{
    public interface IDDScope : IDisposable
    {
        IDDSpan Span { get; }
    }
}
