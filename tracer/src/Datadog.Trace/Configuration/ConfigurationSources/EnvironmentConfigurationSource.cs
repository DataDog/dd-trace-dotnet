// <copyright file="EnvironmentConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that
    /// retrieves values from environment variables.
    /// </summary>
    internal sealed class EnvironmentConfigurationSource : StringConfigurationSource
    {
        /// <inheritdoc />
        public override ConfigurationOrigins Origin => ConfigurationOrigins.EnvVars;

        /// <inheritdoc />
        protected override string? GetString(string key)
        {
            try
            {
// one of the few places where it's legit to use Environment.GetEnvironmentVariable but key must come from ConfigurationKeys or PlatformKeys
#pragma warning disable RS0030
                var value = Environment.GetEnvironmentVariable(key);
#pragma warning restore RS0030

                // The OpenTelemetry spec requires an empty environment variable to be treated as unset
                // https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/#parsing-empty-value
                if (value is { Length: 0 } && key.StartsWith("OTEL_", StringComparison.Ordinal))
                {
                    return null;
                }

                return value;
            }
            catch
            {
                // We should not add a dependency from the Configuration system to the Logger system,
                // so do nothing
            }

            return null;
        }
    }
}
