// <copyright file="BaggageEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tests.Propagators;

/// <summary>
/// A JSON-serializable key/value pair for use with <see cref="Datadog.Trace.TestHelpers.SerializableList{T}"/> in xUnit theory data.
/// </summary>
public class BaggageEntry
{
    public BaggageEntry()
    {
    }

    public BaggageEntry(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; set; }

    public string Value { get; set; }
}
