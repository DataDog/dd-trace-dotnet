// <copyright file="DictionaryValuesProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.Environment;

internal readonly struct DictionaryValuesProvider(Dictionary<string, string>? environmentVariables) : IValueProvider
{
    public string? GetValue(string key, string? defaultValue = null)
    {
        if (environmentVariables is null)
        {
            return defaultValue;
        }

        return environmentVariables.TryGetValue(key, out var value) ? value : null;
    }

    public IDictionary GetValues() => environmentVariables ?? new Dictionary<string, string>();
}
