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
            return Environment.GetEnvironmentVariable(key);
        }
    }
}
