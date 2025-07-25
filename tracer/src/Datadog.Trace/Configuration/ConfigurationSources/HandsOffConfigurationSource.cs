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

internal class HandsOffConfigurationSource(IDictionary<string, ConfigurationEntry> configurations, ConfigurationOrigins origin)
    : StringConfigurationSource
{
    private readonly IDictionary<string, ConfigurationEntry> _configurations = configurations;
    private readonly ConfigurationOrigins _origin = origin;

    internal override ConfigurationOrigins Origin => _origin;

    protected override string? GetString(string key) => _configurations.TryGetValue(key, out var configuration) ? configuration.Value : null;
}
