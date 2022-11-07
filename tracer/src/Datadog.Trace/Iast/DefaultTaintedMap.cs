// <copyright file = "DefaultTaintedMap.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Datadog.Trace.Iast;

internal class DefaultTaintedMap : ITaintedMap
{
    /* Default capacity. It MUST be a power of 2. */
    public const int DefaultCapacity = 1 << 14;
    /* Default flat mode threshold. */
    public const int DefaultFlatModeThresold = 1 << 13;
    /* Periodicity of table purges, as number of put operations. It MUST be a power of two. */
    private static int purgeCount = 1 << 6;
    /* Bitmask for fast modulo with PURGE_COUNT. */
    private static int purgeMask = purgeCount - 1;
    /* Bitmask to convert hashes to positive integers. */
    public const int PositiveMask = int.MaxValue;

    private ConcurrentDictionary<int, ITaintedObject> _map;

    /* Bitmask for fast modulo with table length. */
    private int _lengthMask = 0;
    /* Flag to ensure we do not run multiple purges concurrently. */
    private bool isPurging = false;
    private object _purgingLock = new();
    /*
     * Estimated number of hash table entries. If the hash table switches to flat mode, it stops
     * counting elements.
     */
    private int _estimatedSize;
    /* Reference queue for garbage-collected entries. */

    // TODO: remove estimatesize
    /* Number of elements in the hash table before switching to flat mode. */
    private int _flatModeThreshold;

    /*
     * Default constructor. Uses {@link #DEFAULT_CAPACITY} and {@link #DEFAULT_FLAT_MODE_THRESHOLD}.
     */
    public DefaultTaintedMap()
        : this(DefaultCapacity, DefaultFlatModeThresold)
    {
    }

    /*
     * Create a new hash map with the given capacity and flat mode threshold.
     *
     * @param capacity Capacity of the internal array. It must be a power of 2.
     * @param flatModeThreshold Limit of entries before switching to flat mode.
     */
    private DefaultTaintedMap(int capacity, int flatModeThreshold)
    {
        _map = new ConcurrentDictionary<int, ITaintedObject>();
        _lengthMask = capacity - 1;
        this._flatModeThreshold = flatModeThreshold;
    }

    /*
     * Whether flat mode is enabled or not. Once this is true, it is not set to false again unless
     * {@link #clear()} is called.
     */
    public bool IsFlat { get; private set; } = false;

    /*
     * Returns the {@link TaintedObject} for the given input object.
     *
     * @param key Key object.
     * @return The {@link TaintedObject} if it exists, {@code null} otherwise.
     */
    public ITaintedObject? Get(object key)
    {
        int index = IndexObject(key);

        _map.TryGetValue(index, out var entry);
        while (entry != null)
        {
            if (key == entry.Value)
            {
                return entry;
            }

            entry = entry.Next;
        }

        return null;
    }

    /*
     * Put a new {@link TaintedObject} in the hash table. This method will lose puts in concurrent
     * scenarios.
     *
     * @param entry Tainted object.
     */
    public void Put(ITaintedObject entry)
    {
        int index = Index(entry.PositiveHashCode);

        if (IsFlat)
        {
            // If we flipped to flat mode:
            // - Always override elements ignoring chaining.
            // - Stop updating the estimated size.

            // TODO: Fix the pathological case where all puts have the same identityHashCode (e.g. limit
            // chain length?)
            _map[index] = entry;
            if ((entry.PositiveHashCode & purgeMask) == 0)
            {
                Purge();
            }
        }
        else
        {
            // By default, add the new entry to the head of the chain.
            // We do not control duplicate keys (although we expect they are generally not used).
            _map.TryGetValue(index, out var existingValue);
            entry.Next = existingValue;
            _map[index] = entry;
            if ((entry.PositiveHashCode & purgeMask) == 0)
            {
                // To mitigate the cost of maintaining a thread safe counter, we only update the size every
                // <PURGE_COUNT> puts. This is just an approximation, we rely on key's identity hash code as
                // if it was a random number generator, and we assume duplicate keys are rarely inserted.
                Interlocked.Add(ref _estimatedSize, purgeCount);
                Purge();
            }
        }
    }

    /*
     * Purge entries that have been garbage collected. Only one concurrent call to this method is
     * allowed, further concurrent calls will be ignored.
     */
    internal void Purge()
    {
        // Ensure we enter only once concurrently.
        lock (_purgingLock)
        {
            if (isPurging)
            {
                return;
            }

            isPurging = true;
        }

        try
        {
            // Remove GC'd entries.
            int removedCount = RemoveDeadKeys();

            if (Interlocked.Add(ref _estimatedSize, -removedCount) > _flatModeThreshold)
            {
                IsFlat = true;
            }
        }
        finally
        {
            // Reset purging flag.
            lock (_purgingLock)
            {
                isPurging = false;
            }
        }
    }

    private int RemoveDeadKeys()
    {
        var removed = 0;
        List<int> deadKeys = new();

        foreach (var key in _map.Keys)
        {
            var current = _map[key];
            ITaintedObject? previous = null;

            while (current != null)
            {
                if (!current.IsAlive)
                {
                    if (previous == null)
                    {
                        if (current.Next == null)
                        {
                            deadKeys.Add(key);
                        }
                        else
                        {
                            _map[key] = current.Next;
                        }
                    }
                    else
                    {
                        previous.Next = current.Next;
                    }

                    current = current.Next;
                    removed++;
                }
                else
                {
                    previous = current;
                    current = current.Next;
                }
            }
        }

        foreach (var key in deadKeys)
        {
            _map.TryRemove(key, out _);
        }

        return removed;
    }

    /*
     * Removes a {@link TaintedObject} from the hash table. This method will lose puts in concurrent
     * scenarios.
     *
     * @param entry
     * @return Number of removed elements.
     */
    private int Remove(TaintedObject entry)
    {
        // A remove might be lost when it is concurrent to puts. If that happens, the object will not be
        // removed again, (until the map goes to flat mode). When a remove is lost under concurrency,
        // this method will still return 1, and it will still be subtracted from the map size estimate.
        // If this is infrequent enough, this would lead to a performance degradation of get opertions.
        // If this happens extremely frequently, like number of lost removals close to number of puts,
        // it could prevent the map from ever going into flat mode, and its size might become
        // effectively unbound.
        int index = Index(entry.PositiveHashCode);
        _map.TryGetValue(index, out var cur);

        if (cur is null)
        {
            return 0;
        }

        if (cur == entry)
        {
            if (cur.Next != null)
            {
                _map[index] = cur.Next;
            }
            else
            {
                _map.TryRemove(index, out _);
            }

            return 1;
        }

        var first = cur;
        cur = cur.Next;
        for (ITaintedObject? prev = first; cur != null; prev = cur, cur = cur.Next)
        {
            if (cur == entry)
            {
                prev.Next = cur.Next;
                return 1;
            }
        }

        // If we reach this point, the entry was already removed or put was lost.
        return 0;
    }

    public void Clear()
    {
        IsFlat = false;
        Interlocked.Exchange(ref _estimatedSize, 0);
        _map.Clear();
    }

    private int IndexObject(object obj)
    {
        return Index(PositiveHashCode(IastUtils.IdentityHashCode(obj)));
    }

    private int PositiveHashCode(int h)
    {
        return h & PositiveMask;
    }

    private int Index(int h)
    {
        return h & _lengthMask;
    }

    // For testing only
    internal List<ITaintedObject> ToList()
    {
        List<ITaintedObject> list = new();

        foreach (var value in _map.Values)
        {
            var copy = value;
            while (copy != null)
            {
                list.Add(copy);
                copy = copy.Next;
            }
        }

        return (list);
    }
}
