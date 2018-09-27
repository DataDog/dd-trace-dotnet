using System;

namespace Datadog.Trace.Configuration
{
    public class EnvironmentConfigurationSource : ConfigurationSource
    {
        public override string GetString(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }
    }
}
