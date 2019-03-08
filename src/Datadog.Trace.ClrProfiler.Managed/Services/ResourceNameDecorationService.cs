using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class ResourceNameDecorationService : ISpanDecorationService
    {
        private ResourceNameDecorationService()
        {
        }

        public static ISpanDecorationService Instance { get; } = new ResourceNameDecorationService();

        public void Decorate(ISpan span, ISpanDecorationSource with)
        {
            if (with.TryGetResourceName(out var resourceName))
            {
                span.ResourceName = resourceName;
            }
        }
    }
}
