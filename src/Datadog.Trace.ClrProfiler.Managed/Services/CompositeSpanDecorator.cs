using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class CompositeSpanDecorator : ISpanDecorator
    {
        private readonly List<ISpanDecorator> _spanDecorators;

        public CompositeSpanDecorator(IEnumerable<ISpanDecorator> spanDecorators)
        {
            _spanDecorators = spanDecorators.AsList();
        }

        public CompositeSpanDecorator(params ISpanDecorator[] spanDecorators)
        {
            _spanDecorators = spanDecorators.AsList();
        }

        public void Decorate(ISpan span)
            => _spanDecorators.ForEach(d => d.Decorate(span));
    }
}
