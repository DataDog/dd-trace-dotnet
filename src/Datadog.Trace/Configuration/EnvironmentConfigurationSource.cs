using System;
using Datadog.Trace.Util;

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
            var value = EnvironmentHelpers.GetEnvironmentVariable(key);

            // note: avoid RuntimeInformation.IsOSPlatform(), it is was added in net471
            if (value == null && Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // Env var names are case-sensitive on non-Windows platforms.
                // This fallback fixes an issue where users set an
                // env var name like "DD_TRACE_ADONET_ENABLED"
                // but key is "DD_TRACE_AdoNet_ENABLED".
                return EnvironmentHelpers.GetEnvironmentVariable(key.ToUpperInvariant());
            }

            return value;
        }
    }
}
