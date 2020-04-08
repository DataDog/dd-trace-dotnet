#if !NET45
using MessagePack;
using MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new SpanFormatterResolver();

        private static readonly IMessagePackFormatter<Span> Formatter = new SpanMessagePackFormatter();

        private SpanFormatterResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(Span))
            {
                return (IMessagePackFormatter<T>)Formatter;
            }

            return null;
        }
    }
}
#endif
