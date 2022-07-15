// <copyright file="MultipartPayload.cs" company="Datadog">
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
    internal abstract class MultipartPayload
    {
        internal const int DefaultMaxItemsPerPayload = 100;
        internal const int DefaultMaxBytesPerPayload = 48_000_000;

        private readonly List<MultipartFormItem> _items;
        private readonly IFormatterResolver _formatterResolver;
        private readonly int _maxItemsPerPayload;
        private readonly int _maxBytesPerPayload;
        private long _bytesCount;

        public MultipartPayload(int maxItemsPerPayload = DefaultMaxItemsPerPayload, int maxBytesPerPayload = DefaultMaxBytesPerPayload, IFormatterResolver formatterResolver = null)
        {
            _maxItemsPerPayload = maxItemsPerPayload;
            _maxBytesPerPayload = maxBytesPerPayload;
            _bytesCount = 0;
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _items = new List<MultipartFormItem>(Math.Min(maxItemsPerPayload, DefaultMaxItemsPerPayload));
        }

        public abstract Uri Url { get; }

        public virtual bool HasEvents => _items.Count > 0;

        public int Count => _items.Count;

        public long BytesCount => _bytesCount;

        public abstract bool CanProcessEvent(IEvent @event);

        protected abstract MultipartFormItem CreateMultipartFormItem(ArraySegment<byte> eventInBytes);

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
                if (_items.Count >= _maxItemsPerPayload)
                {
                    return false;
                }

                if (_bytesCount >= _maxBytesPerPayload)
                {
                    return false;
                }

                var eventInBytes = MessagePackSerializer.Serialize(@event, _formatterResolver);
                if (_bytesCount + eventInBytes.Length > _maxBytesPerPayload)
                {
                    return false;
                }

                _items.Add(CreateMultipartFormItem(new ArraySegment<byte>(eventInBytes)));
                _bytesCount += eventInBytes.Length;
                return true;
            }
        }

        public virtual void Clear()
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
                return _items.ToArray();
            }
        }
    }
}
