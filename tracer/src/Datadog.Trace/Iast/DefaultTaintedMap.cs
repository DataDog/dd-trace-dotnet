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

internal sealed class DefaultTaintedMap : ITaintedMap
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
    // Same instance as _map: ICollection.Remove is an atomic compare-and-remove, and
    // TryRemove(KeyValuePair<,>) does not exist on net461/netstandard2.0.
    private ICollection<KeyValuePair<int, ITaintedObject>> _mapAsCollection;
    // Bitmask for fast modulo with table length.
    private int _lengthMask;
    // Flag to ensure we do not run multiple purges concurrently.
    private bool _isPurging;
    private object _purgingLock = new();
    // Number of hash table entries. If the hash table switches to flat mode, it stops counting elements.
    private int _entriesCount;
    /* Number of elements in the hash table before switching to flat mode. */
    private int _flatModeThreshold;

    public DefaultTaintedMap()
    {
        _map = new ConcurrentDictionary<int, ITaintedObject>();
        _mapAsCollection = _map;
        _lengthMask = DefaultCapacity - 1;
        _flatModeThreshold = DefaultFlatModeThresold;
    }

    /// <summary>
    /// Gets a value indicating whether flat mode is enabled or not. Once this is set to true, it is not set to false again unless clear() is called.
    /// The get accessor is only intended for testing purposes.
    /// </summary>
    public bool IsFlat { get; private set; }

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
        bool isString = objectToFind is string;

        while (entry != null)
        {
            if (isString)
            {
                if (objectToFind == entry.Value)
                {
                    return entry;
                }
            }
            else
            {
                if (objectToFind.Equals(entry.Value))
                {
                    return entry;
                }
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
        if (entry is null || (entry.Value is null or ""))
        {
            return;
        }

        var index = Index(entry.PositiveHashCode);

        if (IsFlat)
        {
            // If we flipped to flat mode:
            // - Always override elements ignoring chaining.
            // - Stop updating the estimated size.
            _map[index] = entry;
        }
        else
        {
            // By default, add the new entry to the head of the chain.
            // We do not control duplicate entries.
            // CAS + retry: a plain read-modify-write dropped entries when two callers shared an index.
            while (true)
            {
                if (_map.TryGetValue(index, out var existingValue))
                {
                    entry.Next = existingValue;
                    if (_map.TryUpdate(index, entry, existingValue))
                    {
                        break;
                    }
                }
                else
                {
                    entry.Next = null;
                    if (_map.TryAdd(index, entry))
                    {
                        break;
                    }
                }
            }

            // We only count the entries if we are not in flat mode
            Interlocked.Increment(ref _entriesCount);
        }

        if ((entry.PositiveHashCode & PurgeMask) == 0)
        {
            Purge();
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
                // We only count the entries if we are not in flat mode
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

        foreach (var key in _map.Keys.ToArray())
        {
            if (!_map.TryGetValue(key, out var current))
            {
                // Removed by a concurrent Clear().
                continue;
            }

            ITaintedObject? previous = null;

            while (current is not null)
            {
                var next = current.Next;

                if (current.IsAlive)
                {
                    previous = current;
                }
                else if (previous is not null)
                {
                    // Only ever moves `Next` forward, so a concurrent Get() cannot loop or go backwards.
                    previous.Next = next;
                    removed++;
                }
                else if (next is null)
                {
                    // Compare-and-remove: a plain TryRemove would also delete an entry that a
                    // concurrent Put() published on this key.
                    if (_mapAsCollection.Remove(new KeyValuePair<int, ITaintedObject>(key, current)))
                    {
                        removed++;
                    }

                    break;
                }
                else if (_map.TryUpdate(key, next, current))
                {
                    removed++;
                }
                else
                {
                    // A concurrent Put() replaced the head; its chain still holds the dead nodes,
                    // so the next purge picks them up.
                    break;
                }

                current = next;
            }
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

    public int GetEstimatedSize()
    {
        return _entriesCount;
    }

    // For testing only
    public List<ITaintedObject> GetListValues()
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
