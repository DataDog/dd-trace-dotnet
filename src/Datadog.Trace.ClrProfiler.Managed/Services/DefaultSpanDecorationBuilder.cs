using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class DefaultSpanDecorationBuilder : ISpanDecorationBuilder
    {
        private readonly List<ISpanDecorator> _decorations = new List<ISpanDecorator>();

        private DefaultSpanDecorationBuilder()
        {
        }

        public static DefaultSpanDecorationBuilder Create() => new DefaultSpanDecorationBuilder();

        public ISpanDecorationBuilder With(ISpanDecorator decoration)
        {
            _decorations.Add(decoration);

            return this;
        }

        public ISpanDecorator Build() => new CompositeSpanDecorator(_decorations);
    }
}
