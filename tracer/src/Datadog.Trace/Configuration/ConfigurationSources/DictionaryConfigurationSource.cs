// <copyright file="DictionaryConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

internal class DictionaryConfigurationSource : StringConfigurationSource
{
    private readonly IReadOnlyDictionary<string, string> _dictionary;

    public DictionaryConfigurationSource(IReadOnlyDictionary<string, string> dictionary)
    {
        _dictionary = dictionary;
    }

    internal override ConfigurationOrigins Origin => ConfigurationOrigins.Code;

    public override string? GetString(string key)
    {
        _dictionary.TryGetValue(key, out var value);
        return value;
    }
}
