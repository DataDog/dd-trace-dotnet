using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    internal static class ConfigurationSourceExtensions
    {
        public static string GetStringWithFallbacks(this IConfigurationSource source, string key, string formatReplacement = null)
        {
            return GetAllKeys(key, formatReplacement)
                  .Select(source.GetString)
                  .FirstOrDefault(value => value != null);
        }

        public static bool? GetBoolWithFallbacks(this IConfigurationSource source, string key, string formatReplacement = null)
        {
            return GetAllKeys(key, formatReplacement)
                  .Select(source.GetBool)
                  .FirstOrDefault(value => value != null);
        }

        public static double? GetDoubleWithFallbacks(this IConfigurationSource source, string key, string formatReplacement = null)
        {
            return GetAllKeys(key, formatReplacement)
                  .Select(source.GetDouble)
                  .FirstOrDefault(value => value != null);
        }

        private static IEnumerable<string> GetAllKeys(string key, string formatReplacement)
        {
            // try to expand a single key into all its fallbacks
            IEnumerable<string> keys = ConfigurationKeyFallbacks.Instance.GetKeys(key);

            // if specified, try to replace "{0}" in key templates
            return formatReplacement != null ? keys.Select(s => string.Format(s, formatReplacement)) : keys;
        }
    }
}
