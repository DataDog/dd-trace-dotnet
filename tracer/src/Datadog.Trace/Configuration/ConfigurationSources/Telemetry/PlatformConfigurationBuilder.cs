// <copyright file="PlatformConfigurationBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal readonly struct PlatformConfigurationBuilder(IConfigurationTelemetry telemetry)
{
    private readonly IConfigurationTelemetry _telemetry = telemetry;
    private readonly IConfigurationSource _environmentConfigurationSource = ConfigurationBuilder.GetEnvironmentConfigurationSource();

    /// <summary>
    /// Platform key builder
    /// </summary>
    /// <param name="key">The key must come from the class PlatformKeys</param>
    /// <returns>ConfigurationBuilder.HasKeys</returns>
    public ConfigurationBuilder.HasKeys WithKeys(string key) => new(_environmentConfigurationSource, _telemetry, key);
}
