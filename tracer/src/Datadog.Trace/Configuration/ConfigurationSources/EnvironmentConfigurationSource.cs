// <copyright file="EnvironmentConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Represents a configuration source that
/// retrieves values from environment variables.
/// </summary>
internal class EnvironmentConfigurationSource : StringConfigurationSource
{
    internal static readonly EnvironmentConfigurationSource Instance = new();

    /// <inheritdoc />
    public override ConfigurationOrigins Origin => ConfigurationOrigins.EnvVars;

    /// <inheritdoc />
    protected override string? GetString(string key)
    {
// only place where it's allowed
#pragma warning disable RS0030
        return Environment.GetEnvironmentVariable(key);
#pragma warning restore RS0030
    }
}
