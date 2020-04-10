using MessagePack;
using MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal struct FormatterResolverWrapper : IFormatterResolver
    {
        private readonly IFormatterResolver _resolver;

        public FormatterResolverWrapper(IFormatterResolver resolver)
        {
            _resolver = resolver;
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return _resolver.GetFormatter<T>();
        }
    }
}
