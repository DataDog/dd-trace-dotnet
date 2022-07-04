// <copyright file="CIVisibilityMultipartPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal abstract class CIVisibilityMultipartPayload
    {
        private readonly List<MultipartFormItem> _items;
        private readonly IFormatterResolver _formatterResolver;
        private readonly int _maxItems;

        public CIVisibilityMultipartPayload(int maxItems = 10, IFormatterResolver formatterResolver = null)
        {
            _maxItems = 10;
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _items = new List<MultipartFormItem>(10);
        }

        public abstract Uri Url { get; }

        public bool HasEvents => _items.Count > 0;

        public int Count => _items.Count;

        public abstract bool CanProcessEvent(IEvent @event);

        protected abstract MultipartFormItem CreateMultipartFormItem(ArraySegment<byte> eventInBytes);

        public bool TryProcessEvent(IEvent @event)
        {
            lock (_items)
            {
                if (_items.Count > _maxItems)
                {
                    return false;
                }

                var eventInBytes = MessagePackSerializer.Serialize(@event, _formatterResolver);
                _items.Add(CreateMultipartFormItem(new ArraySegment<byte>(eventInBytes)));
                return true;
            }
        }

        public void Clear()
        {
            lock (_items)
            {
                _items.Clear();
            }
        }

        public MultipartFormItem[] ToArray()
        {
            lock (_items)
            {
                return _items.ToArray();
            }
        }
    }
}
