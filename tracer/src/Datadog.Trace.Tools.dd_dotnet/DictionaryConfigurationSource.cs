// <copyright file="DictionaryConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class DictionaryConfigurationSource : IConfigurationSource
{
    private readonly IReadOnlyDictionary<string, string> _dictionary;

    public DictionaryConfigurationSource(IReadOnlyDictionary<string, string> dictionary)
    {
        _dictionary = dictionary;
    }

    public string? GetString(string key)
    {
        if (_dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }
}
