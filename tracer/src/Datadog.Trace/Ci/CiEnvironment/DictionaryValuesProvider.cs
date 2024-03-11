// <copyright file="DictionaryValuesProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

internal readonly struct DictionaryValuesProvider : IValueProvider
{
    private readonly Dictionary<string, string>? _environmentVariables;

    public DictionaryValuesProvider(Dictionary<string, string>? environmentVariables)
    {
        _environmentVariables = environmentVariables;
    }

    public string? GetValue(string key, string? defaultValue = null)
    {
        if (_environmentVariables is null)
        {
            return defaultValue;
        }

        return _environmentVariables.TryGetValue(key, out var value) ? value : null;
    }

    public IDictionary GetValues() => _environmentVariables ?? new Dictionary<string, string>();
}
