// <copyright file="CIVisibilityMultipartPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly int _maxBytes;
        private long _bytesCount;

        public CIVisibilityMultipartPayload(int maxItems = 10, int maxBytes = 48_000_000, IFormatterResolver formatterResolver = null)
        {
            _maxItems = 10;
            _maxBytes = maxBytes;
            _bytesCount = 0;
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _items = new List<MultipartFormItem>(10);
        }

        public abstract Uri Url { get; }

        public bool HasEvents => _items.Count > 0;

        public int Count => _items.Count;

        public long BytesCount => _bytesCount;

        public abstract bool CanProcessEvent(IEvent @event);

        protected abstract MultipartFormItem CreateMultipartFormItem(ArraySegment<byte> eventInBytes);

        protected virtual void OnBeforeToArray()
        {
        }

        protected void AddMultipartFormItem(MultipartFormItem item)
        {
            lock (_items)
            {
                _items.Add(item);
            }
        }

        public bool TryProcessEvent(IEvent @event)
        {
            lock (_items)
            {
                if (_items.Count >= _maxItems)
                {
                    return false;
                }

                if (_bytesCount >= _maxBytes)
                {
                    return false;
                }

                var eventInBytes = MessagePackSerializer.Serialize(@event, _formatterResolver);
                if (_bytesCount + eventInBytes.Length > _maxBytes)
                {
                    return false;
                }

                _items.Add(CreateMultipartFormItem(new ArraySegment<byte>(eventInBytes)));
                _bytesCount += eventInBytes.Length;
                return true;
            }
        }

        public void Clear()
        {
            lock (_items)
            {
                _items.Clear();
                _bytesCount = 0;
            }
        }

        public MultipartFormItem[] ToArray()
        {
            lock (_items)
            {
                OnBeforeToArray();
                return _items.ToArray();
            }
        }
    }
}
