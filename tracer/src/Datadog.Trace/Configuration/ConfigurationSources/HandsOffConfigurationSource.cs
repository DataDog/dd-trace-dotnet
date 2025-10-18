// <copyright file="HandsOffConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog.HandsOffConfiguration;

namespace Datadog.Trace.Configuration.ConfigurationSources;

internal sealed class HandsOffConfigurationSource(Dictionary<string, string> configurations, bool localFile)
    : StringConfigurationSource
{
    private readonly Dictionary<string, string> _configurations = configurations;
    private readonly bool _localFile = localFile;

    public override ConfigurationOrigins Origin => _localFile ? ConfigurationOrigins.LocalStableConfig : ConfigurationOrigins.FleetStableConfig;

    protected override string? GetString<TKey>(TKey key)
    {
        _configurations.TryGetValue(key.GetKey(), out var value);
        return value;
    }
}
