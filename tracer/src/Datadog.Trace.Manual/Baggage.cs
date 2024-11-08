// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
public static class Baggage
{
    private static readonly NoopDictionary NoopDictionaryInstance = new();

    /// <summary>
    /// Gets or sets the baggage collection for the current execution context.
    /// </summary>
    [Instrumented]
    public static IDictionary<string, string?> Current
    {
        get
        {
            // auto-instrumentation will return Trace.Baggage.Current instead
            return NoopDictionaryInstance;
        }

        set
        {
            // auto-instrumentation will set Trace.Baggage.Current instead
            _ = value;
        }
    }

    private sealed class NoopDictionary : IDictionary<string, string?>
    {
        int ICollection<KeyValuePair<string, string?>>.Count => 0;

        bool ICollection<KeyValuePair<string, string?>>.IsReadOnly => true;

        ICollection<string> IDictionary<string, string?>.Keys { get; } = [];

        ICollection<string?> IDictionary<string, string?>.Values { get; } = [];

        string? IDictionary<string, string?>.this[string key]
        {
            get => null;
            set { }
        }

        IEnumerator<KeyValuePair<string, string?>> IEnumerable<KeyValuePair<string, string?>>.GetEnumerator()
            => Enumerable.Empty<KeyValuePair<string, string?>>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => Enumerable.Empty<KeyValuePair<string, string?>>().GetEnumerator();

        void ICollection<KeyValuePair<string, string?>>.Add(KeyValuePair<string, string?> item)
        {
        }

        void ICollection<KeyValuePair<string, string?>>.Clear()
        {
        }

        bool ICollection<KeyValuePair<string, string?>>.Contains(KeyValuePair<string, string?> item) => false;

        void ICollection<KeyValuePair<string, string?>>.CopyTo(KeyValuePair<string, string?>[] array, int arrayIndex)
        {
        }

        bool ICollection<KeyValuePair<string, string?>>.Remove(KeyValuePair<string, string?> item) => false;

        bool IDictionary<string, string?>.ContainsKey(string key) => false;

        void IDictionary<string, string?>.Add(string key, string? value)
        {
        }

        bool IDictionary<string, string?>.Remove(string key) => false;

        bool IDictionary<string, string?>.TryGetValue(string key, out string? value)
        {
            value = null;
            return false;
        }
    }
}
