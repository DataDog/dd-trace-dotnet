// <copyright file="TestHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class TestHeadersCollection : IBinaryHeadersCollection
{
    public Dictionary<string, byte[]> Values { get; } = new();

    public byte[] TryGetBytes(string name)
        => Values.TryGetValue(name, out var value) ? value : null;

    public void Add(string name, byte[] value)
        => Values[name] = value;
}
