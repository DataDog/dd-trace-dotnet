// <copyright file = "DefaultTaintedMap2.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.ExtensionMethods;

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
    private Random _random = new();

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
        _map = new();
        _lengthMask = capacity - 1;
        this._flatModeThreshold = flatModeThreshold;
    }

    /// <summary>
    /// Returns the TaintedObject for the given input object.
    /// </summary>
    /// <param name="objectToFind">The object that should be found in the map</param>
    /// <returns>The retrieved tainted object or null</returns>
    public ITaintedObject? Get(object objectToFind)
    {
        if (objectToFind is null)
        {
            return null;
        }

        _map.TryGetValue(objectToFind, out var entry);
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
        if (entry?.Value is null || (entry.Value as string == string.Empty))
        {
            return;
        }

        var count = _map.Count;
        if (count > _flatModeThreshold)
        {
            RemoveRandomValue(count);
        }

        _map.TryAdd(new WeakMapReference(entry.Value), entry);
        if ((entry.PositiveHashCode & purgeMask) == 0)
        {
            Purge();
        }
    }

    private void RemoveRandomValue(int count)
    {
        int index = _random.Next(count);
        _map.TryRemove(_map.ElementAt(index).Key, out _);
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
            if ((key as WeakMapReference)?.IsAlive == false || !_map[key].IsAlive)
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
        _map.Clear();
    }

    // For testing only
    public List<ITaintedObject> ToList()
    {
        return _map.Values.ToList();
    }
}
