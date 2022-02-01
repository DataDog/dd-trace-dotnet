// <copyright file="EventsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent
{
    internal abstract class EventsPayload
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<EventsPayload>();

        private readonly EventsBuffer<IEvent> _events;
        private readonly IFormatterResolver _formatterResolver;

        public EventsPayload(IFormatterResolver formatterResolver = null)
        {
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _events = new EventsBuffer<IEvent>(1024 * 1024 * 4, _formatterResolver);
        }

        public abstract Uri Url { get; }

        public bool HasEvents => _events.Count > 0;

        public int Count => _events.Count;

        internal EventsBuffer<IEvent> Events => _events;

        public abstract bool CanProcessEvent(IEvent @event);

        public bool TryProcessEvent(IEvent @event)
        {
            return _events.TryWrite(@event);
        }

        public void Clear()
        {
            _events.Clear();
        }

        public byte[] ToArray()
        {
            return MessagePackSerializer.Serialize(this, _formatterResolver);
        }
    }
}
