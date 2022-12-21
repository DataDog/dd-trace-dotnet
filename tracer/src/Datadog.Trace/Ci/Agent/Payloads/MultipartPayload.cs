// <copyright file="MultipartPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal abstract class MultipartPayload : EventPlatformPayload
    {
        internal const int DefaultMaxItemsPerPayload = 100;
        internal const int DefaultMaxBytesPerPayload = 48_000_000;
        internal const int HeaderSize = 1024;

        private readonly EventsBuffer<IEvent> _events;
        private readonly List<MultipartFormItem> _items;
        private readonly IFormatterResolver _formatterResolver;
        private readonly int _maxItemsPerPayload;
        private readonly int _maxBytesPerPayload;

        public MultipartPayload(CIVisibilitySettings settings, int maxItemsPerPayload = DefaultMaxItemsPerPayload, int maxBytesPerPayload = DefaultMaxBytesPerPayload, IFormatterResolver formatterResolver = null)
            : base(settings)
        {
            if (maxBytesPerPayload < HeaderSize)
            {
                ThrowHelper.ThrowArgumentException($"Buffer size should be at least {HeaderSize}", nameof(maxBytesPerPayload));
            }

            _maxItemsPerPayload = maxItemsPerPayload;
            _maxBytesPerPayload = maxBytesPerPayload;
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _items = new List<MultipartFormItem>(Math.Min(maxItemsPerPayload, DefaultMaxItemsPerPayload));

            // Because we don't know the size of the events array envelope we left 1024kb for that.
            _events = new EventsBuffer<IEvent>(Math.Min(_maxBytesPerPayload, DefaultMaxBytesPerPayload) - HeaderSize, _formatterResolver);
        }

        public override bool HasEvents => _events.Count > 0;

        public override int Count => _items.Count + (_events.Count > 0 ? 1 : 0);

        internal EventsBuffer<IEvent> Events => _events;

        protected abstract MultipartFormItem CreateMultipartFormItem(EventsBuffer<IEvent> eventsBuffer);

        protected void AddMultipartFormItem(MultipartFormItem item)
        {
            lock (_items)
            {
                if (_items.Count < _maxItemsPerPayload - 1)
                {
                    _items.Add(item);
                }
            }
        }

        public override bool TryProcessEvent(IEvent @event) => _events.TryWrite(@event);

        public override void Reset()
        {
            lock (_items)
            {
                _events.Clear();
                _items.Clear();
            }
        }

        public MultipartFormItem[] ToArray()
        {
            lock (_items)
            {
                _items.Add(CreateMultipartFormItem(_events));
                return _items.ToArray();
            }
        }
    }
}
