// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
public class Baggage
{
    private static readonly AsyncLocal<Baggage> AsyncStorage = new();

    internal ConcurrentDictionary<string, string>? Items { get; private set; }

    /// <summary>
    /// Gets or sets the current baggage.
    /// </summary>
    public static Baggage Current
    {
        get => AsyncStorage.Value ??= new Baggage();
        set => AsyncStorage.Value = value;
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
    public IEnumerable<KeyValuePair<string, string>> GetAll()
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

    internal void Replace(Baggage? baggage)
    {
        Items = baggage?.Items;
    }
}
