// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
public class Baggage
{
    private static readonly Baggage _current = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Baggage"/> class.
    /// </summary>
    public Baggage()
    {
    }

    /// <summary>
    /// Gets the baggage collection for the current execution context.
    /// </summary>
    [Instrumented]
    public static Baggage Current
    {
        get => _current;
    }

    /// <summary>
    /// Gets a value indicating whether this baggage collection is empty.
    /// </summary>
    [Instrumented]
    public bool IsEmpty => true;

    /// <summary>
    /// Gets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <returns>Returns the baggage item value, or <c>null</c> if not found.</returns>
    [Instrumented]
    public string? Get(string name)
    {
        return string.Empty;
    }

    /// <summary>
    /// Gets all baggage values.
    /// </summary>
    /// <returns>All baggage values.</returns>
    [Instrumented]
    public KeyValuePair<string, string>[] GetAll()
    {
        return [];
    }

    /// <summary>
    /// Sets the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <param name="value">The baggage item value.</param>
    [Instrumented]
    public void Set(string name, string? value)
    {
    }

    /// <summary>
    /// Removes the baggage value associated with the specified name.
    /// </summary>
    /// <param name="name">The baggage item name.</param>
    /// <returns><c>true</c> if the object was removed successfully; otherwise, <c>false</c>.</returns>
    [Instrumented]
    public bool Remove(string name)
    {
        return false;
    }

    /// <summary>
    /// Removes all baggage values.
    /// </summary>
    [Instrumented]
    public void Clear()
    {
    }
}
