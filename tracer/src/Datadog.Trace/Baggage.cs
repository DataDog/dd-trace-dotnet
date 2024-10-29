// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
internal class Baggage : IDictionary<string, string>
{
    private static readonly AsyncLocal<Baggage> AsyncStorage = new();

    private static readonly List<KeyValuePair<string, string>> EmptyList = [];

    private List<KeyValuePair<string, string>>? _items;

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
    public Baggage(IEnumerable<KeyValuePair<string, string>> items)
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

    bool ICollection<KeyValuePair<string, string>>.IsReadOnly => false;

    ICollection<string> IDictionary<string, string>.Keys
    {
        get
        {
            if (_items is { Count: > 0 } items)
            {
                var keys = new string[items.Count];

                for (int i = 0; i < items.Count; i++)
                {
                    keys[i] = items[i].Key;
                }

                return keys;
            }

            return [];
        }
    }

    ICollection<string> IDictionary<string, string>.Values
    {
        get
        {
            if (_items is { Count: > 0 } items)
            {
                var values = new string[items.Count];

                for (int i = 0; i < items.Count; i++)
                {
                    values[i] = items[i].Value;
                }

                return values;
            }

            return [];
        }
    }

    public string this[string key]
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

    private List<KeyValuePair<string, string>> EnsureListInitialized()
    {
        if (_items == null)
        {
            Interlocked.CompareExchange(ref _items, [], null);
        }

        return _items;
    }

    bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
    {
        if (item.Key == null!)
        {
            ThrowHelper.ThrowArgumentException("The key cannot be null.", nameof(item));
        }

        if (_items is { Count: > 0 } list)
        {
            lock (list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    // match both key and value
                    if (list[i].Key == item.Key && list[i].Value == item.Value)
                    {
                        list.RemoveAt(i);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="key">The baggage item name.</param>
    /// <param name="value">The baggage item value.</param>
    public void AddOrReplace(string key, string value)
    {
        var list = EnsureListInitialized();

        lock (list)
        {
            AddOrReplace(list, key, value);
        }
    }

    private static void AddOrReplace(List<KeyValuePair<string, string>> list, string key, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Key == key)
            {
                // key found, replace with new value
                list[i] = new KeyValuePair<string, string>(key, value);
                return;
            }
        }

        // key not found, add new entry
        list.Add(new KeyValuePair<string, string>(key, value));
    }

    public bool TryGetValue(string key, out string value)
    {
        if (_items is { Count: > 0 } list)
        {
            lock (list)
            {
                foreach (var pair in list)
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

    bool IDictionary<string, string>.ContainsKey(string key)
    {
        if (_items is { Count: > 0 } items)
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

    public void Add(string key, string value)
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

            items.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
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

        if (_items is { Count: > 0 } list)
        {
            lock (list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Key == name)
                    {
                        list.RemoveAt(i);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool Contains(KeyValuePair<string, string> item)
    {
        throw new System.NotImplementedException();
    }

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        throw new System.NotImplementedException();
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
    /// Gets all baggage values.
    /// </summary>
    /// <returns>A new array that contains all baggage values.</returns>
    public KeyValuePair<string, string>[] GetAllItems()
    {
        if (_items is { } list)
        {
            lock (list)
            {
                if (list.Count > 0)
                {
                    return _items.ToArray();
                }
            }
        }

        return [];
    }

    /// <summary>
    /// Removes all baggage items.
    /// </summary>
    public void Clear()
    {
        if (_items is { } list)
        {
            lock (list)
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

        if (Count == 0)
        {
            // nothing to merge
            return;
        }

        var sourceItems = _items!; // if count > 0, then _items is not null
        var destinationItems = destination.EnsureListInitialized();

        lock (sourceItems)
        {
            lock (destinationItems)
            {
                foreach (var sourceItem in sourceItems)
                {
                    AddOrReplace(destinationItems, sourceItem.Key, sourceItem.Value);
                }
            }
        }
    }

    private List<KeyValuePair<string, string>>.Enumerator GetEnumerator() => _items?.GetEnumerator() ?? EmptyList.GetEnumerator();

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
