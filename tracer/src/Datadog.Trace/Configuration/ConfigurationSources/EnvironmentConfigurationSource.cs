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
        private readonly IDictionary _environmentVariables;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentConfigurationSource"/> class.
        /// </summary>
        [PublicApi]
        public EnvironmentConfigurationSource()
        {
            try
            {
                _environmentVariables = Environment.GetEnvironmentVariables();
            }
            catch
            {
                // We should not add a dependency from the Configuration system to the Logger system,
                // so do nothing
                _environmentVariables = new Dictionary<string, string>();
            }

            TelemetryFactory.Metrics.Record(PublicApiUsage.EnvironmentConfigurationSource_Ctor);
        }

        private protected EnvironmentConfigurationSource(bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is to give us a non-public API we can use
            try
            {
                _environmentVariables = Environment.GetEnvironmentVariables();
            }
            catch
            {
                // We should not add a dependency from the Configuration system to the Logger system,
                // so do nothing
                _environmentVariables = new Dictionary<string, string>();
            }
        }

        /// <inheritdoc />
        internal override ConfigurationOrigins Origin { get; } = ConfigurationOrigins.EnvVars;

        /// <inheritdoc />
        [PublicApi]
        public override string? GetString(string key)
        {
            return _environmentVariables[key]?.ToString();
        }
    }
}
