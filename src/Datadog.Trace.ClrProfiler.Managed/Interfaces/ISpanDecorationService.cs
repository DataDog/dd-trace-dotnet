using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface ISpanDecorationService
    {
        void Decorate(ISpan span, ISpanDecorationSource with);
    }
}
