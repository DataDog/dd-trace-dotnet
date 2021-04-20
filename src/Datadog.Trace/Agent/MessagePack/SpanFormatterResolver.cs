using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanFormatterResolver : IFormatterResolver
    {
        private readonly IMessagePackFormatter<Span> _formatter;

        internal SpanFormatterResolver(IKeepRateCalculator keepRateCalculator)
        {
            _formatter = new SpanMessagePackFormatter(keepRateCalculator);
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(Span))
            {
                return (IMessagePackFormatter<T>)_formatter;
            }

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
