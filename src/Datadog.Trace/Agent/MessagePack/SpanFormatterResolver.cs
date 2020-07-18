using System.Diagnostics;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new SpanFormatterResolver();

        private static readonly IMessagePackFormatter<Span> SpanFormatter = new SpanMessagePackFormatter();

        private static readonly IMessagePackFormatter<Activity> ActivityFormatter = new ActivityMessagePackFormatter();

        private static readonly IMessagePackFormatter<TraceActivitiesContainer> TraceActivityContainerFormatter = new TraceActivitiesContainerMessagePackFormatter();

        private SpanFormatterResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(Span))
            {
                return (IMessagePackFormatter<T>)SpanFormatter;
            }
            else if (typeof(T) == typeof(TraceActivitiesContainer))
            {
                return (IMessagePackFormatter<T>)TraceActivityContainerFormatter;
            }
            else if (typeof(T) == typeof(Activity))
            {
                return (IMessagePackFormatter<T>)ActivityFormatter;
            }

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
