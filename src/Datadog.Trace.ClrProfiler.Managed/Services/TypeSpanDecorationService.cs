using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class TypeSpanDecorationService : ISpanDecorationService
    {
        private TypeSpanDecorationService()
        {
        }

        public static ISpanDecorationService Instance { get; } = new TypeSpanDecorationService();

        public void Decorate(ISpan span, ISpanDecorationSource with)
        {
            if (with.TryGetType(out var spanType))
            {
                span.Type = spanType;
            }
        }
    }
}
