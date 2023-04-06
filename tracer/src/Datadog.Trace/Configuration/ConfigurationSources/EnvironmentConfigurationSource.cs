// <copyright file="EnvironmentConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that
    /// retrieves values from environment variables.
    /// </summary>
    public class EnvironmentConfigurationSource : StringConfigurationSource
    {
        /// <inheritdoc />
        public override string GetString(string key)
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
