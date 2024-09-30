// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Util;

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
        if (items != null!)
        {
            Items = new OrderedKeyValuePairList<string, string>(items);
        }
    }

    private OrderedKeyValuePairList<string, string>? Items { get; set; }

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
    public int Count => Items?.Count ?? 0;

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
        return Items?.ToArray() ?? [];
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
            Items ??= new OrderedKeyValuePairList<string, string>();
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

        return Items?.Remove(name, out _) ?? false;
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
            Items = new OrderedKeyValuePairList<string, string>(baggage.Items);
            return;
        }

        foreach (var item in baggage.Items)
        {
            Items[item.Key] = item.Value;
        }
    }
}
