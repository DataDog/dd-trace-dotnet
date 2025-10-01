// <copyright file="MockEnvironmentVariableProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Managed.Loader;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader;

public class MockEnvironmentVariableProvider : IEnvironmentVariableProvider
{
    private readonly Dictionary<string, string> _variables = new();

    public void SetEnvironmentVariable(string key, string? value)
    {
        if (value is null)
        {
            _variables.Remove(key);
        }
        else
        {
            _variables[key] = value;
        }
    }

    public string? GetEnvironmentVariable(string key)
    {
        return _variables.TryGetValue(key, out var value) ? value : null;
    }

    public void Clear()
    {
        _variables.Clear();
    }
}
