// <copyright file="CIFormatterResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CIFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new CIFormatterResolver();

        private readonly IMessagePackFormatter<Span> _spanFormatter;
        private readonly IMessagePackFormatter<EventsPayload> _eventsPayloadFormatter;
        private readonly IMessagePackFormatter<IEvent> _eventFormatter;
        private readonly IMessagePackFormatter<TestEvent> _testEventFormatter;
        private readonly IMessagePackFormatter<SpanEvent> _spanEventFormatter;

        private CIFormatterResolver()
        {
            _spanFormatter = SpanMessagePackFormatter.Instance;
            _eventsPayloadFormatter = new CIEventMessagePackFormatter(CIVisibility.Settings.TracerSettings);
            _eventFormatter = new IEventMessagePackFormatter();
            _testEventFormatter = new TestEventMessagePackFormatter();
            _spanEventFormatter = new SpanEventMessagePackFormatter();
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(Span))
            {
                return (IMessagePackFormatter<T>)_spanFormatter;
            }

            if (typeof(T) == typeof(EventsPayload))
            {
                return (IMessagePackFormatter<T>)_eventsPayloadFormatter;
            }

            if (typeof(T) == typeof(IEvent))
            {
                return (IMessagePackFormatter<T>)_eventFormatter;
            }

            if (typeof(T) == typeof(TestEvent))
            {
                return (IMessagePackFormatter<T>)_testEventFormatter;
            }

            if (typeof(T) == typeof(SpanEvent))
            {
                return (IMessagePackFormatter<T>)_spanEventFormatter;
            }

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
