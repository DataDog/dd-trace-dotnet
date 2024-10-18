// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
internal class Baggage
{
    private static readonly AsyncLocal<Baggage> AsyncStorage = new();

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
        Items = new ConcurrentDictionary<string, string>(items);
    }

    internal ConcurrentDictionary<string, string>? Items { get; private set; }

    /// <summary>
    /// Gets the baggage collection for the current execution context.
    /// </summary>
    public static Baggage Current
    {
        get => AsyncStorage.Value ??= new Baggage();
    }

    /// <summary>
    /// Gets a value indicating whether this baggage collection is empty.
    /// </summary>
    public bool IsEmpty => Items is null or { IsEmpty: true };

    /// <summary>
    /// Merge the baggage items from <paramref name="b1"/> and
    /// <paramref name="b2"/> into a new baggage instance.
    /// </summary>
    /// <returns>
    /// A new <see cref="Baggage"/> instance containing all the items
    /// from <paramref name="b1"/> and <paramref name="b2"/>.
    /// </returns>
    /// <remarks>
    /// If a baggage item with the same key exists in both <paramref name="b1"/> and <paramref name="b2"/>,
    /// the value from <paramref name="b2"/> will be used.
    /// </remarks>
    public static Baggage Merge(Baggage? b1, Baggage? b2)
    {
        var items1 = b1?.Items;
        var items2 = b2?.Items;

        var items1IsEmpty = items1 is null || items1.IsEmpty;
        var items2IsEmpty = items2 is null || items2.IsEmpty;

        if (items1IsEmpty && items2IsEmpty)
        {
            return new Baggage();
        }

        if (items1IsEmpty)
        {
            return new Baggage(items2!);
        }

        if (items2IsEmpty)
        {
            return new Baggage(items1!);
        }

        var result = new Baggage(items1!);
        result.Merge(b2);
        return result;
    }

    /// <summary>
    /// Gets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <returns>Returns the baggage item value, or <c>null</c> if not found.</returns>
    public string? Get(string name)
    {
        if (name == null!)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(name));
        }

        if (Items is null)
        {
            return null;
        }

        Items.TryGetValue(name, out var value);
        return value;
    }

    /// <summary>
    /// Gets all baggage values.
    /// </summary>
    /// <returns>All baggage values.</returns>
    public KeyValuePair<string, string>[] GetAll()
    {
        return Items is null ? [] : Items.ToArray();
    }

    /// <summary>
    /// Sets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <param name="value">The baggage item value.</param>
    public void Set(string name, string? value)
    {
        if (name == null!)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(name));
        }

        if (value is null)
        {
            Remove(name);
        }
        else
        {
            Items ??= new ConcurrentDictionary<string, string>();
            Items[name] = value;
        }
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

        return Items?.TryRemove(name, out _) ?? false;
    }

    /// <summary>
    /// Removes all baggage values.
    /// </summary>
    public void Clear()
    {
        Items?.Clear();
    }

    /// <summary>
    /// Adds the baggage items from <paramref name="baggage"/> to this baggage.
    /// </summary>
    public void Merge(Baggage? baggage)
    {
        if (baggage?.Items is null)
        {
            // nothing to add
            return;
        }

        if (Items is null)
        {
            // initialize with the items from the other baggage
            Items = new ConcurrentDictionary<string, string>(baggage.Items);
            return;
        }

        foreach (var item in baggage.Items)
        {
            Items[item.Key] = item.Value;
        }
    }
}
