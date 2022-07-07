// <copyright file="CIVisibilityProtocolPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal abstract class CIVisibilityProtocolPayload : EvpPayload
    {
        private readonly EventsBuffer<IEvent> _events;
        private readonly IFormatterResolver _formatterResolver;

        public CIVisibilityProtocolPayload(IFormatterResolver formatterResolver = null)
        {
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;

            // Because we don't know the size of the events array envelope we left 500kb for that.
            _events = new EventsBuffer<IEvent>(Ci.CIVisibility.Settings.MaximumAgentlessPayloadSize - (500 * 1024), _formatterResolver);
        }

        public bool HasEvents => _events.Count > 0;

        public int Count => _events.Count;

        internal EventsBuffer<IEvent> Events => _events;

        public abstract bool CanProcessEvent(IEvent @event);

        public bool TryProcessEvent(IEvent @event) => _events.TryWrite(@event);

        public void Clear() => _events.Clear();

        public byte[] ToArray() => MessagePackSerializer.Serialize(this, _formatterResolver);
    }
}
