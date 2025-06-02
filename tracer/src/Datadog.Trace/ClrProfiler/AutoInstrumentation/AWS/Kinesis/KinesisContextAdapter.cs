// <copyright file="KinesisContextAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;

internal struct KinesisContextAdapter : IHeadersCollection, IBinaryHeadersCollection
{
    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<KinesisContextAdapter>();
    private readonly Dictionary<string, List<string>> headers = new();

    public KinesisContextAdapter()
    {
    }

    public Dictionary<string, object> GetDictionary()
    {
        // Convert to Dictionary<string, object> to satisfy IHeadersCollection
        return headers.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
    }

    public IEnumerable<string> GetValues(string name)
    {
        if (headers.TryGetValue(name, out var value))
        {
            return value;
        }

        return Enumerable.Empty<string>();
    }

    public void Set(string name, string value)
    {
        headers[name] = new List<string> { value };
    }

    public void Add(string name, string value)
    {
        if (headers.TryGetValue(name, out var oldValues))
        {
            oldValues.Add(value);
        }
        else
        {
            headers[name] = new List<string> { value };
        }
    }

    public void Remove(string name)
    {
        headers.Remove(name);
    }

    public byte[] TryGetLastBytes(string name)
    {
        if (headers.TryGetValue(name, out var value))
        {
            return Convert.FromBase64String(value[value.Count - 1]);
        }

        return new byte[0];
    }

    public void Add(string name, byte[] value)
    {
        Add(name, Convert.ToBase64String(value));
    }

    public void SetDictionary(Dictionary<string, object> dictionary)
    {
        Console.WriteLine($"[KinesisContextAdapter] SetDictionary called with dictionary: {(dictionary == null ? "null" : $"count={dictionary.Count}")}");

        if (dictionary == null)
        {
            Console.WriteLine("[KinesisContextAdapter] Dictionary is null, returning early");
            return;
        }

        Console.WriteLine("[KinesisContextAdapter] Clearing existing headers");
        headers.Clear();

        foreach (var kvp in dictionary)
        {
            Console.WriteLine($"[KinesisContextAdapter] Processing key: {kvp.Key}, value type: {kvp.Value?.GetType().Name ?? "null"}");

            if (kvp.Value is string stringValue)
            {
                Console.WriteLine($"[KinesisContextAdapter] Setting string value: {stringValue}");
                Set(kvp.Key, stringValue);
            }
            else if (kvp.Value is byte[] bytes)
            {
                Console.WriteLine($"[KinesisContextAdapter] Setting byte array of length: {bytes.Length}");
                Add(kvp.Key, bytes);
            }
            else if (kvp.Value != null)
            {
                var stringRepresentation = kvp.Value.ToString();
                Console.WriteLine($"[KinesisContextAdapter] Converting to string: {stringRepresentation}");
                Set(kvp.Key, stringRepresentation);
            }
            else
            {
                Console.WriteLine("[KinesisContextAdapter] Skipping null value");
            }
        }

        Console.WriteLine($"[KinesisContextAdapter] SetDictionary complete. Final header count: {headers.Count}");
        foreach (var header in headers)
        {
            Console.WriteLine($"[KinesisContextAdapter] Header: {header.Key} = {string.Join(", ", header.Value)}");
        }
    }
}
