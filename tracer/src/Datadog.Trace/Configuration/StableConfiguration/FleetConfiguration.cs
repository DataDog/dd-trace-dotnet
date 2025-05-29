// <copyright file="FleetConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.LibDatadog.LibraryConfig;

namespace Datadog.Trace.Configuration.StableConfiguration;

internal class FleetConfiguration(LibraryConfigName configName, string configValue, LibraryConfigSource source)
{
    public LibraryConfigName ConfigName { get; } = configName;

    public string ConfigValue { get; } = configValue;

    public LibraryConfigSource Source { get; } = source;
}
