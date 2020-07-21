using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Helper class used to expand a configuration key
    /// into tis multiple supported fallback options.
    /// </summary>
    internal sealed class ConfigurationKeyFallbacks : IEnumerable<IReadOnlyList<string>>
    {
        private readonly Dictionary<string, string[]> _mapping = new Dictionary<string, string[]>();

        private ConfigurationKeyFallbacks()
        {
        }

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static ConfigurationKeyFallbacks Instance { get; }
            = new ConfigurationKeyFallbacks
              {
                  { ConfigurationKeys.ConfigurationFileName, "DD_DOTNET_TRACER_CONFIG_FILE" },
                  { ConfigurationKeys.Integrations.Enabled, "DD_{0}_ENABLED" },
                  { ConfigurationKeys.Integrations.AnalyticsEnabled, "DD_{0}_ANALYTICS_ENABLED" },
                  { ConfigurationKeys.Integrations.AnalyticsSampleRate, "DD_{0}_ANALYTICS_SAMPLING_RATE" }
              };

        /// <summary>
        /// Expands the specified <paramref name="key"/>
        /// into its multiple supported fallback options.
        /// </summary>
        /// <param name="key">The key to get fallbacks for.</param>
        /// <returns>The supported keys, including <paramref name="key"/> as the first item.</returns>
        public IReadOnlyList<string> GetKeys(string key)
        {
            return _mapping.TryGetValue(key, out var fallbacks) ? fallbacks : new[] { key };
        }

        IEnumerator<IReadOnlyList<string>> IEnumerable<IReadOnlyList<string>>.GetEnumerator()
        {
            return _mapping.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _mapping.Values.GetEnumerator();
        }

        private void Add(params string[] keys)
        {
            // use first item as the lookup key
            _mapping.Add(keys[0], keys);
        }
    }
}
