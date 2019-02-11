using System;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class ActionBlockSpanDecorator : ISpanDecorator
    {
        private readonly Action<ISpan> _block;

        public ActionBlockSpanDecorator(Action<ISpan> block)
        {
            _block = block;
        }

        public void Decorate(ISpan span)
            => _block(span);
    }
}
