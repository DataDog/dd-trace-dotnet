// <copyright file="ExposureCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Exposure.Model;

namespace Datadog.Trace.FeatureFlags.Exposure;

/// <summary>
/// Esta clase no es thread-safe intencionalmente.
/// La seguridad de hilos se gestiona en el patrón de acceso de un solo hilo.
/// </summary>
internal sealed class ExposureCache
{
    private readonly int _capacity;
    private readonly Dictionary<Key, Value> _cache;
    private readonly LinkedList<Key> _lruList;

    public ExposureCache(int capacity)
    {
        _capacity = capacity;
        _cache = new Dictionary<Key, Value>(capacity);
        _lruList = new LinkedList<Key>();
    }

    public int Size => _lruList.Count;

    public bool Add(ExposureEvent exposureEvent)
    {
        lock (_cache)
        {
            var key = new Key(exposureEvent);
            var value = new Value(exposureEvent);

            bool exists = _cache.TryGetValue(key, out var oldValue);

            if (exists)
            {
                // Update LRU priority (move element to the begining of the queue)
                _lruList.Remove(key);
                _lruList.AddFirst(key);

                if (oldValue == value)
                {
                    return false;
                }
            }

            // New key or different value
            if (!exists && _cache.Count >= _capacity)
            {
                RemoveLeastRecentlyUsed();
            }

            _cache[key] = value;
            if (!exists)
            {
                _lruList.AddFirst(key);
            }

            return true;
        }
    }

    public Value? Get(Key key)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                // Get operation should refresh LRU priority
                _lruList.Remove(key);
                _lruList.AddFirst(key);
                return value;
            }

            return null;
        }
    }

    private void RemoveLeastRecentlyUsed()
    {
        var last = _lruList.Last;
        if (last != null)
        {
            _cache.Remove(last.Value);
            _lruList.RemoveLast();
        }
    }

    public sealed record Key
    {
        public Key(ExposureEvent exposureEvent)
        {
            Flag = exposureEvent.Flag.Key;
            Subject = exposureEvent.Subject.Id;
        }

        public string Flag { get; }

        public string Subject { get; }
    }

    public sealed record Value
    {
        public Value(ExposureEvent exposureEvent)
        {
            Variant = exposureEvent.Variant.Key;
            Allocation = exposureEvent.Allocation.Key;
        }

        public string Variant { get; }

        public string Allocation { get; }
    }
}
