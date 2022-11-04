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

    private TaintedObject?[] table;

    /* Bitmask for fast modulo with table length. */
    private int lengthMask = 0;
    /* Flag to ensure we do not run multiple purges concurrently. */
    private bool isPurging = false;
    private object _purgingLock = new();
    /*
     * Estimated number of hash table entries. If the hash table switches to flat mode, it stops
     * counting elements.
     */
    private int estimatedSize;
    /* Reference queue for garbage-collected entries. */

    // TODO: make it taintedobject instead of iweakaware
    // TODO: remove estimatesize
    // TODO: hacer locked todo el map
    // TODO: usar map no concurrente???
    private ConcurrentQueue<IWeakAware> referenceList = new();

    /* Number of elements in the hash table before switching to flat mode. */
    private int flatModeThreshold;

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
        table = new TaintedObject[capacity];
        lengthMask = table.Length - 1;
        this.flatModeThreshold = flatModeThreshold;
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
    public TaintedObject? Get(object key)
    {
        int index = IndexObject(key);
        TaintedObject? entry = table[index];
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
    public void Put(TaintedObject entry)
    {
        int index = Index(entry.PositiveHashCode);

        if (IsFlat)
        {
            // If we flipped to flat mode:
            // - Always override elements ignoring chaining.
            // - Stop updating the estimated size.

            // TODO: Fix the pathological case where all puts have the same identityHashCode (e.g. limit
            // chain length?)
            table[index] = entry;
            if ((entry.PositiveHashCode & purgeMask) == 0)
            {
                Purge();
            }
        }
        else
        {
            // By default, add the new entry to the head of the chain.
            // We do not control duplicate keys (although we expect they are generally not used).
            entry.Next = table[index];
            table[index] = entry;
            if ((entry.PositiveHashCode & purgeMask) == 0)
            {
                // To mitigate the cost of maintaining a thread safe counter, we only update the size every
                // <PURGE_COUNT> puts. This is just an approximation, we rely on key's identity hash code as
                // if it was a random number generator, and we assume duplicate keys are rarely inserted.
                Interlocked.Add(ref estimatedSize, purgeCount);
                Purge();
            }
        }

        referenceList.Enqueue(entry);
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

            if (Interlocked.Add(ref estimatedSize, -removedCount) > flatModeThreshold)
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
        var alive = new List<IWeakAware>();
        while (referenceList.TryDequeue(out var key))
        {
            if (key.IsAlive)
            {
                alive.Add(key);
            }
            else
            {
                if (key is TaintedObject tainted)
                {
                    Remove(tainted);
                    removed++;
                }
            }
        }

        foreach (var value in alive)
        {
            referenceList.Enqueue(value);
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
        TaintedObject? cur = table[index];

        if (cur is null)
        {
            return 0;
        }

        if (cur == entry)
        {
            table[index] = cur.Next;
            return 1;
        }

        var first = cur;
        cur = cur.Next;
        for (TaintedObject? prev = first; cur != null; prev = cur, cur = cur.Next)
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
        Interlocked.Exchange(ref estimatedSize, 0);

        for (int i = 0; i < table.Length; i++)
        {
            table[i] = null;
        }

        referenceList = new();
    }

    public ConcurrentQueue<IWeakAware> GetReferenceQueue()
    {
        return referenceList;
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
        return h & lengthMask;
    }

    // For testing only
    internal List<IWeakAware> TableToList()
    {
        List<IWeakAware> list = new();
        foreach (var element in table)
        {
            if (element != null)
            {
                var item = element;
                do
                {
                    list.Add(item);
                }
                while ((item = item.Next) != null);
            }
        }

        return list;
    }

    /*
        private Iterator<TaintedObject> iterator(int start, int stop)
        {
            return new Iterator<TaintedObject>()
            {
            int currentIndex = start;
            TaintedObject currentSubPos;

            public bool hasNext()
            {
                if (currentSubPos != null)
                {
                    return true;
                }
                for (; currentIndex < stop; currentIndex++)
                {
                    if (table[currentIndex] != null)
                    {
                        return true;
                    }
                }
                return false;
            }

            public TaintedObject Next()
            {
                if (currentSubPos != null)
                {
                    TaintedObject toReturn = currentSubPos;
                    currentSubPos = toReturn.Next;
                    return toReturn;
                }

                for (; currentIndex < stop; currentIndex++)
                {
                    final TaintedObject entry = table[currentIndex];
                    if (entry != null)
                    {
                        currentSubPos = entry.next;
                        currentIndex++;
                        return entry;
                    }
                }
                throw new NoSuchElementException();
            }
        };
    }

    public Iterator<TaintedObject> iterator()
    {
        return iterator(0, table.length);
    }
    */
}
