// <copyright file="EnvironmentConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentConfigurationSource"/> class.
        /// </summary>
        [PublicApi]
        public EnvironmentConfigurationSource()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.EnvironmentConfigurationSource_Ctor);
        }

        private protected EnvironmentConfigurationSource(bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is to give us a non-public API we can use
        }

        /// <inheritdoc />
        internal override ConfigurationOrigins Origin { get; } = ConfigurationOrigins.EnvVars;

        /// <inheritdoc />
        [PublicApi]
        public override string? GetString(string key)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
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
