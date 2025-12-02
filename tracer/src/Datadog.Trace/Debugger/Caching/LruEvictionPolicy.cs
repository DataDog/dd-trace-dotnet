// <copyright file="LruEvictionPolicy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Debugger.Caching
{
    internal sealed class LruEvictionPolicy<TKey> : IEvictionPolicy<TKey>
        where TKey : notnull
    {
        private readonly LinkedList<TKey> _list = new LinkedList<TKey>();
        private readonly Dictionary<TKey, LinkedListNode<TKey>> _map = new Dictionary<TKey, LinkedListNode<TKey>>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public void Add(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_map.ContainsKey(key))
                {
                    var node = _list.AddFirst(key);
                    _map[key] = node;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void Remove(TKey key)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _map.Remove(key);
            }
        }

        public void Access(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _list.Remove(node);
                    _list.AddFirst(node);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public TKey Evict()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_list.Last == null)
                {
                    throw new InvalidOperationException("Cache is empty");
                }

                var key = _list.Last.Value;
                Remove(key);
                return key;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
