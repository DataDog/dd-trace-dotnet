// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
internal sealed class Baggage : IDictionary<string, string?>
{
    private static readonly AsyncLocal<Baggage> AsyncStorage = new();

    private static readonly List<KeyValuePair<string, string?>> EmptyList = [];

    private List<KeyValuePair<string, string?>>? _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="Baggage"/> class.
    /// </summary>
    public Baggage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Baggage"/> class using the specified items.
    /// </summary>
    /// <param name="items">The baggage items.</param>
    public Baggage(IEnumerable<KeyValuePair<string, string?>>? items)
    {
        if (items != null!)
        {
            _items = [..items];
        }
    }

    /// <summary>
    /// Gets or sets the baggage collection for the current execution context.
    /// </summary>
    public static Baggage Current
    {
        get => AsyncStorage.Value ??= new Baggage();
        set => AsyncStorage.Value = value;
    }

    /// <summary>
    /// Gets the count of items in this baggage instance.
    /// </summary>
    public int Count => _items?.Count ?? 0;

    bool ICollection<KeyValuePair<string, string?>>.IsReadOnly => false;

    ICollection<string> IDictionary<string, string?>.Keys
    {
        get
        {
            if (_items is { } items)
            {
                lock (items)
                {
                    if (items.Count > 0)
                    {
                        var keys = new string[items.Count];

                        for (int i = 0; i < items.Count; i++)
                        {
                            keys[i] = items[i].Key;
                        }

                        return keys;
                    }
                }
            }

            return [];
        }
    }

    ICollection<string?> IDictionary<string, string?>.Values
    {
        get
        {
            if (_items is { } items)
            {
                lock (items)
                {
                    if (items.Count > 0)
                    {
                        var values = new string?[items.Count];

                        for (int i = 0; i < items.Count; i++)
                        {
                            values[i] = items[i].Value;
                        }

                        return values;
                    }
                }
            }

            return [];
        }
    }

    public string? this[string key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            ThrowHelper.ThrowKeyNotFoundException($"The key was not found: {key}");
            return default!; // unreachable
        }

        set
        {
            AddOrReplace(key, value);
        }
    }

    private List<KeyValuePair<string, string?>> EnsureListInitialized()
    {
        if (_items == null)
        {
            Interlocked.CompareExchange(ref _items, [], null);
        }

        return _items;
    }

    /// <summary>
    /// Sets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="key">The baggage item name.</param>
    /// <param name="value">The baggage item value.</param>
    public void AddOrReplace(string key, string? value)
    {
        var items = EnsureListInitialized();

        lock (items)
        {
            AddOrReplaceInternal(items, key, value);
        }
    }

    private static void AddOrReplaceInternal(List<KeyValuePair<string, string?>> items, string key, string? value)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Key == key)
            {
                // key found, replace with new value
                items[i] = new KeyValuePair<string, string?>(key, value);
                return;
            }
        }

        // key not found, add new entry
        items.Add(new KeyValuePair<string, string?>(key, value));
    }

    public bool TryGetValue(string key, out string value)
    {
        if (_items is { } items)
        {
            lock (items)
            {
                foreach (var pair in items)
                {
                    if (pair.Key == key)
                    {
                        value = pair.Value!;
                        return true;
                    }
                }
            }
        }

        value = default!;
        return false;
    }

    bool IDictionary<string, string?>.ContainsKey(string key)
    {
        if (_items is { } items)
        {
            lock (items)
            {
                foreach (var item in items)
                {
                    if (item.Key == key)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    bool ICollection<KeyValuePair<string, string?>>.Contains(KeyValuePair<string, string?> item)
    {
        if (_items is { } items)
        {
            lock (items)
            {
                foreach (var existingItem in items)
                {
                    if (existingItem.Key == item.Key && existingItem.Value == item.Value)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void Add(string key, string? value)
    {
        var items = EnsureListInitialized();

        lock (items)
        {
            foreach (var item in items)
            {
                if (item.Key == key)
                {
                    ThrowHelper.ThrowArgumentException("An element with the same key already exists.", nameof(key));
                }
            }

            items.Add(new KeyValuePair<string, string?>(key, value));
        }
    }

    void ICollection<KeyValuePair<string, string?>>.Add(KeyValuePair<string, string?> item)
    {
        Add(item.Key, item.Value);
    }

    /// <summary>
    /// Removes the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <returns><c>true</c> if the object was removed successfully; otherwise, <c>false</c>.</returns>
    public bool Remove(string name)
    {
        if (name == null!)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(name));
        }

        if (_items is { } items)
        {
            lock (items)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Key == name)
                    {
                        items.RemoveAt(i);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    bool ICollection<KeyValuePair<string, string?>>.Remove(KeyValuePair<string, string?> item)
    {
        if (_items is { } items)
        {
            lock (items)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Key == item.Key && items[i].Value == item.Value)
                    {
                        items.RemoveAt(i);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    void ICollection<KeyValuePair<string, string?>>.CopyTo(KeyValuePair<string, string?>[] array, int arrayIndex)
    {
        if (array == null!)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || arrayIndex >= array.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (_items is { } items)
        {
            lock (items)
            {
                // check if items fit into the array
                if (array.Length - arrayIndex < items.Count)
                {
                    ThrowHelper.ThrowArgumentException(
                        """
                        The number of elements in the source collection is greater than 
                        the available space from arrayIndex to the end of the destination array.
                        """);
                }

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    array[arrayIndex + i] = item;
                }
            }
        }
    }

    /// <summary>
    /// Gets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <returns>Returns the baggage item value, or <c>null</c> if not found.</returns>
    public string? GetValueOrDefault(string name)
    {
        if (name == null!)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(name));
        }

        return TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// Removes all baggage items.
    /// </summary>
    public void Clear()
    {
        if (_items is { } items)
        {
            lock (items)
            {
                _items?.Clear();
            }
        }
    }

    /// <summary>
    /// Adds the baggage items from this baggage instance into <paramref name="destination"/>.
    /// </summary>
    public void MergeInto(Baggage destination)
    {
        if (destination == null!)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(destination));
        }

        if (ReferenceEquals(this, destination))
        {
            // We're trying to merge with ourselves
            return;
        }

        var sourceItems = _items;

        if (sourceItems is null || sourceItems.Count == 0)
        {
            // nothing to merge
            return;
        }

        var destinationItems = destination.EnsureListInitialized();

        object lock1;
        object lock2;

        // ensure we lock the two objects in the same order regardless of
        // which one is source vs destination to avoid a deadlock if:
        // Thread 1: baggageA.MergeInto(baggageB);
        // Thread 2: baggageB.MergeInto(baggageA);
        if (RuntimeHelpers.GetHashCode(sourceItems) < RuntimeHelpers.GetHashCode(destinationItems))
        {
            lock1 = sourceItems;
            lock2 = destinationItems;
        }
        else
        {
            lock1 = destinationItems;
            lock2 = sourceItems;
        }

        lock (lock1)
        {
            lock (lock2)
            {
                foreach (var sourceItem in sourceItems)
                {
                    AddOrReplaceInternal(destinationItems, sourceItem.Key, sourceItem.Value);
                }
            }
        }
    }

    public void Enumerate<T>(T processor)
        where T : struct, ICancellableObserver<KeyValuePair<string, string?>>
    {
        if (_items is { } list)
        {
            lock (list)
            {
                foreach (var item in list)
                {
                    if (processor.CancellationRequested)
                    {
                        break;
                    }

                    processor.OnNext(item);
                }

                processor.OnCompleted();
            }
        }
    }

    private List<KeyValuePair<string, string?>>.Enumerator GetEnumerator() => _items?.GetEnumerator() ?? EmptyList.GetEnumerator();

    IEnumerator<KeyValuePair<string, string?>> IEnumerable<KeyValuePair<string, string?>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
