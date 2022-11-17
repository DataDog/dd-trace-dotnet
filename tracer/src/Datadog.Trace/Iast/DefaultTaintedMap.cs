// <copyright file = "DefaultTaintedMap.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Iast;

internal class DefaultTaintedMap : ITaintedMap
{
    // Default capacity. It MUST be a power of 2.
    public const int DefaultCapacity = 1 << 14;
    // Default flat mode threshold.
    public const int DefaultFlatModeThresold = 1 << 13;
    // Bitmask to convert hashes to positive integers.
    public const int PositiveMask = int.MaxValue;
    // Periodicity of table purges, as number of put operations. It MUST be a power of two.
    private const int PurgeCount = 1 << 6;
    // Bitmask for fast modulo with PURGE_COUNT.
    private const int PurgeMask = PurgeCount - 1;

    // Map containing the tainted objects
    private ConcurrentDictionary<int, ITaintedObject> _map;

    // Bitmask for fast modulo with table length.
    private int _lengthMask = 0;
    // Flag to ensure we do not run multiple purges concurrently.
    private bool _isPurging = false;
    private object _purgingLock = new();

    // Number of hash table entries. If the hash table switches to flat mode, it stops counting elements.
    private int _entriesCount;

    /* Number of elements in the hash table before switching to flat mode. */
    private int _flatModeThreshold;

    public DefaultTaintedMap()
    {
        _map = new ConcurrentDictionary<int, ITaintedObject>();
        _lengthMask = DefaultCapacity - 1;
        _flatModeThreshold = DefaultFlatModeThresold;
    }

    /// <summary>
    /// Gets a value indicating whether flat mode is enabled or not. Once this is set to true, it is not set to false again unless clear() is called.
    /// </summary>
    public bool IsFlat { get; private set; } = false;

    /// <summary>
    /// Returns the ITaintedObject for the given input object.
    /// </summary>
    /// <param name="objectToFind">The object that should be found in the map</param>
    /// <returns>The retrieved tainted object or null</returns>
    public ITaintedObject? Get(object objectToFind)
    {
        if (objectToFind is null)
        {
            return null;
        }

        _map.TryGetValue(IndexObject(objectToFind), out var entry);

        while (entry != null)
        {
            if (objectToFind == entry.Value)
            {
                return entry;
            }

            entry = entry.Next;
        }

        return null;
    }

    /// <summary>
    /// Put a new TaintedObject in the dictionary.
    /// </summary>
    /// <param name="entry">Tainted object</param>
    public void Put(ITaintedObject entry)
    {
        if (entry is null || (entry.Value as string == string.Empty))
        {
            return;
        }

        var index = Index(entry.PositiveHashCode);

        if (!IsFlat)
        {
            // By default, add the new entry to the head of the chain.
            // We do not control duplicate entries.
            _map.TryGetValue(index, out var existingValue);
            entry.Next = existingValue;
        }

        // If we flipped to flat mode:
        // - Always override elements ignoring chaining.
        // - Stop updating the estimated size.

        _map[index] = entry;

        if ((entry.PositiveHashCode & PurgeMask) == 0)
        {
            Purge();
        }

        Interlocked.Increment(ref _entriesCount);
    }

    /// <summary>
    /// Purge entries that have been garbage collected. Only one concurrent call to this method is
    /// allowed, further concurrent calls will be ignored.
    /// </summary>
    internal void Purge()
    {
        // Ensure we enter only once concurrently.
        lock (_purgingLock)
        {
            if (_isPurging)
            {
                return;
            }

            _isPurging = true;
        }

        try
        {
            // Remove GC'd entries.
            var removedCount = RemoveDeadKeys();

            if (!IsFlat)
            {
                if (Interlocked.Add(ref _entriesCount, -removedCount) > _flatModeThreshold)
                {
                    IsFlat = true;
                }
            }
        }
        finally
        {
            // Reset purging flag.
            lock (_purgingLock)
            {
                _isPurging = false;
            }
        }
    }

    private int RemoveDeadKeys()
    {
        var removed = 0;
        List<int> deadKeys = new();
        ITaintedObject? previous;

        foreach (var key in _map.Keys)
        {
            var current = _map[key];
            previous = null;

            while (current is not null)
            {
                if (!current.IsAlive)
                {
                    if (previous is null)
                    {
                        // We can delete the map key
                        if (current.Next is null)
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

    public void Clear()
    {
        IsFlat = false;
        Interlocked.Exchange(ref _entriesCount, 0);
        _map.Clear();
    }

    private int IndexObject(object objectStored)
    {
        return Index(PositiveHashCode(IastUtils.IdentityHashCode(objectStored)));
    }

    private int PositiveHashCode(int hash)
    {
        return hash & PositiveMask;
    }

    public int Index(int hash)
    {
        return hash & _lengthMask;
    }

    // For testing only
    public List<ITaintedObject> ToList()
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
