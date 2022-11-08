// <copyright file = "DefaultTaintedMap2.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Datadog.Trace.Iast;

internal class DefaultTaintedMap2 : ITaintedMap
{
    // Default capacity. It MUST be a power of 2.
    public const int DefaultCapacity = 1 << 14;
    // Default flat mode threshold.
    public const int DefaultFlatModeThresold = 1 << 13;
    // Periodicity of table purges, as number of put operations. It MUST be a power of two.
    private static int purgeCount = 1 << 6;
    // Bitmask for fast modulo with PURGE_COUNT.
    private static int purgeMask = purgeCount - 1;
    // Bitmask to convert hashes to positive integers.
    public const int PositiveMask = int.MaxValue;

    // Map containing the tainted objects
    private ConcurrentDictionary<object, ITaintedObject> _map;

    // Bitmask for fast modulo with table length.
    private int _lengthMask = 0;
    // Flag to ensure we do not run multiple purges concurrently.
    private bool isPurging = false;
    private object _purgingLock = new();

    /* Number of elements in the hash table before switching to flat mode. */
    private int _flatModeThreshold;

    public DefaultTaintedMap2()
        : this(DefaultCapacity, DefaultFlatModeThresold)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaintedMap2"/> class.
    /// </summary>
    /// <param name="capacity">Capacity of the internal map. It must be a power of 2.</param>
    /// <param name="flatModeThreshold">Limit of entries before switching to flat mode.</param>
    /// <returns>The retrieved tainted object or null</returns>
    private DefaultTaintedMap2(int capacity, int flatModeThreshold)
    {
        _map = new ConcurrentDictionary<int, ITaintedObject>();
        _lengthMask = capacity - 1;
        this._flatModeThreshold = flatModeThreshold;
    }

    /// <summary>
    /// Gets a value indicating whether whether flat mode is enabled or not. Once this is true, it is not set to false again unless clear is called.
    /// </summary>
    public bool IsFlat { get; private set; } = false;

    /// <summary>
    /// Returns the TaintedObject for the given input object.
    /// </summary>
    /// <param name="objectToFind">The object that should be found in the map</param>
    /// <returns>The retrieved tainted object or null</returns>
    public ITaintedObject? Get(object objectToFind)
    {
        int index = IndexObject(objectToFind);

        _map.TryGetValue(index, out var entry);
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
    /// Put a new {@link TaintedObject} in the hash table. This method will lose puts in concurrent scenarios.
    /// </summary>
    /// <param name="entry">Tainted object</param>
    public void Put(ITaintedObject entry)
    {
        if (entry.Value is null)
        {
            return;
        }

        if (IsFlat)
        {
            // If we flipped to flat mode:
            // - Always override elements ignoring chaining.
            // - Stop updating the estimated size.

            _map[index] = entry;
            if ((entry.PositiveHashCode & purgeMask) == 0)
            {
                Purge();
            }
        }
        else
        {
            _map.TryAdd(index, entry);
            if ((entry.PositiveHashCode & purgeMask) == 0)
            {
                Purge();
            }
        }
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
            if (isPurging)
            {
                return;
            }

            isPurging = true;
        }

        try
        {
            // Remove GC'd entries.
            var removedCount = RemoveDeadKeys();

            if (_map.Count > _flatModeThreshold)
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
        List<object> deadKeys = new();

        var f = _map.Count;

        foreach (var key in _map.Keys)
        {
            var current = _map[key];

            if (!current.IsAlive)
            {
                deadKeys.Add(key);
                removed++;
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
        _map.Clear();
    }

    // For testing only
    internal List<ITaintedObject> ToList()
    {
        return _map.Values.ToList();
    }
}
