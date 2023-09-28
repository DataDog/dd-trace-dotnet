// <copyright file="CompositeConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class CompositeConfigurationSource : IConfigurationSource
{
    private readonly List<IConfigurationSource> _sources = new();

    public void Add(IConfigurationSource source)
    {
        _sources.Add(source);
    }

    public string? GetString(string key)
    {
        foreach (var source in _sources)
        {
            var value = source.GetString(key);

            if (value != null)
            {
                return value;
            }
        }

        return null;
    }
}
