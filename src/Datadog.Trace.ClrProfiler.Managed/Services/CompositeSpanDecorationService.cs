using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class CompositeSpanDecorationService : ISpanDecorationService
    {
        private readonly List<ISpanDecorationService> _spanDecorators;

        public CompositeSpanDecorationService(IEnumerable<ISpanDecorationService> spanDecorators)
        {
            _spanDecorators = spanDecorators.ToList();
        }

        public CompositeSpanDecorationService(params ISpanDecorationService[] spanDecorators)
        {
            _spanDecorators = spanDecorators.ToList();
        }

        public void Decorate(ISpan span, ISpanDecorationSource with)
        {
            foreach (var decorator in _spanDecorators)
            {
                decorator.Decorate(span, with);
            }
        }
    }
}
