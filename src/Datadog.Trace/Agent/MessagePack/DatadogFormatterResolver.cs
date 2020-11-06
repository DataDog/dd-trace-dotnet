using Datadog.Trace.Agent.NamedPipes;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class DatadogFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new DatadogFormatterResolver();

        private static readonly IMessagePackFormatter<TraceRequest> TraceRequestFormatter = new TraceRequestMessagePackFormatter();
        private static readonly IMessagePackFormatter<Span> SpanFormatter = new SpanMessagePackFormatter();

        private DatadogFormatterResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(Span))
            {
                return (IMessagePackFormatter<T>)SpanFormatter;
            }

            if (typeof(T) == typeof(TraceRequest))
            {
                return (IMessagePackFormatter<T>)TraceRequestFormatter;
            }

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
