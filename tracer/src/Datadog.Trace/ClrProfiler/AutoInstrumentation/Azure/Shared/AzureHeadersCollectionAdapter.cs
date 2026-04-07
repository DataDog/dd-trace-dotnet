// <copyright file="AzureHeadersCollectionAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;

internal readonly struct AzureHeadersCollectionAdapter : IHeadersCollection
{
    private readonly IDictionary<string, object> _properties;

    public AzureHeadersCollectionAdapter(IDictionary<string, object> properties)
    {
        _properties = properties;
    }

    public void Add(string name, string value)
    {
        _properties[name] = value;
    }

    public IEnumerable<string> GetValues(string name)
    {
        if (_properties.TryGetValue(name, out var value) && value is string s)
        {
            return new[] { s };
        }

        return Array.Empty<string>();
    }

    public void Remove(string name)
    {
        _properties.Remove(name);
    }

    public void Set(string name, string value)
    {
        _properties[name] = value;
    }
}
