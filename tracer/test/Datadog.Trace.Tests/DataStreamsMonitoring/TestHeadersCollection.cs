// <copyright file="TestHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class TestHeadersCollection : IHeadersCollection
{
    public Dictionary<string, string> Values { get; } = new();

    public IEnumerable<string> GetValues(string name)
        => Values.TryGetValue(name, out var value) ? new[] { value } : null;

    public void Add(string name, string value)
        => Values[name] = value;

    public void Set(string name, string value)
        => Values[name] = value;

    public void Remove(string name)
        => Values.Remove(name);
}
