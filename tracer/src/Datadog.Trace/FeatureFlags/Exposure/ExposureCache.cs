// <copyright file="ExposureCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Exposure.Model;

namespace Datadog.Trace.FeatureFlags.Exposure;

/// <summary> LRU ExposureEvents cache </summary>
internal sealed class ExposureCache
{
    private readonly int _capacity;

    // Maps each subject to its LRU node, so a repeat moves in O(1).
    private readonly Dictionary<Key, LinkedListNode<Entry>> _cache;
    private readonly LinkedList<Entry> _lruList;

    public ExposureCache(int capacity)
    {
        _capacity = capacity;
        _cache = new Dictionary<Key, LinkedListNode<Entry>>(capacity);
        _lruList = new LinkedList<Entry>();
    }

    public int Size => _lruList.Count;

    public bool Add(ExposureEvent exposureEvent)
    {
        lock (_cache)
        {
            var key = new Key(exposureEvent);
            if (_cache.TryGetValue(key, out var node))
            {
                // Update LRU priority (move the node to the beginning of the list)
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                if (Matches(node.Value.Recorded, in exposureEvent))
                {
                    return false;
                }

                node.Value = new Entry(node.Value.Key, new Value(exposureEvent));
                return true;
            }

            var entry = new Entry(key, new Value(exposureEvent));
            if (_cache.Count >= _capacity && _lruList.Last is { } evicted)
            {
                // Reuse the evicted node, so a full cache allocates no node.
                _cache.Remove(evicted.Value.Key);
                _lruList.RemoveLast();
                evicted.Value = entry;
                _lruList.AddFirst(evicted);
                _cache[key] = evicted;
                return true;
            }

            _cache[key] = _lruList.AddFirst(entry);
            return true;
        }
    }

    /// <summary>True when the subject has this exact exposure recorded. Does not change the LRU order.</summary>
    public bool Contains(ExposureEvent exposureEvent)
    {
        lock (_cache)
        {
            return _cache.TryGetValue(new Key(exposureEvent), out var node) && Matches(node.Value.Recorded, in exposureEvent);
        }
    }

    // Same as Value equality, without allocating a Value.
    private static bool Matches(Value recorded, in ExposureEvent exposureEvent)
        => recorded.Variant == exposureEvent.Variant.Key
           && recorded.Allocation == exposureEvent.Allocation.Key
           && recorded.SerialId == exposureEvent.SerialId;

    private readonly record struct Entry(Key Key, Value Recorded);

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
            SerialId = exposureEvent.SerialId;
        }

        public string Variant { get; }

        public string Allocation { get; }

        public long? SerialId { get; }
    }
}
