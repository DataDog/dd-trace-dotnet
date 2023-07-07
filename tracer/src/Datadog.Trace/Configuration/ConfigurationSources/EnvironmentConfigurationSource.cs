// <copyright file="EnvironmentConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that
    /// retrieves values from environment variables.
    /// </summary>
    public class EnvironmentConfigurationSource : StringConfigurationSource
    {
        private readonly Dictionary<string, string?> _environmentVariables;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentConfigurationSource"/> class.
        /// </summary>
        [PublicApi]
        public EnvironmentConfigurationSource()
        {
            _environmentVariables = FillEnvironmentVariables();
            TelemetryFactory.Metrics.Record(PublicApiUsage.EnvironmentConfigurationSource_Ctor);
        }

        private protected EnvironmentConfigurationSource(bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is to give us a non-public API we can use
            _environmentVariables = FillEnvironmentVariables();
        }

        /// <inheritdoc />
        internal override ConfigurationOrigins Origin { get; } = ConfigurationOrigins.EnvVars;

        /// <inheritdoc />
        [PublicApi]
        public override string? GetString(string key)
        {
            if (_environmentVariables.TryGetValue(key, out var value))
            {
                return value;
            }

            return default;
        }

        private Dictionary<string, string?> FillEnvironmentVariables()
        {
            var envVar = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (DictionaryEntry? entry in Environment.GetEnvironmentVariables())
                {
                    if (entry is null) { continue; }
                    envVar[entry.Value.Key?.ToString() ?? string.Empty] = entry.Value.Value?.ToString();
                }
            }
            catch
            {
                // We should not add a dependency from the Configuration system to the Logger system,
                // so do nothing
            }

            return envVar;
        }
    }
}
