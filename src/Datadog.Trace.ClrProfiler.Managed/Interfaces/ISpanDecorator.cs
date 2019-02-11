using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface ISpanDecorator
    {
        void Decorate(ISpan span);
    }
}
