using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Extension methods for <see cref="IConfigurationSource"/>.
    /// </summary>
    public static class ConfigurationSourceExtensions
    {
        /// <summary>
        /// Gets the <see cref="string"/> value of the setting with the specified keys.
        /// Will try keys in order until a match is found.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to search for keys.</param>
        /// <param name="keys">The keys that identify the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public static string GetString(this IConfigurationSource source, IEnumerable<string> keys)
        {
            return keys.Select(source.GetString)
                       .FirstOrDefault(value => value != null);
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value of the setting with the specified keys.
        /// Will try keys in order until a match is found.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to search for keys.</param>
        /// <param name="keys">The keys that identify the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public static bool? GetBool(this IConfigurationSource source, IEnumerable<string> keys)
        {
            return keys.Select(source.GetBool)
                       .FirstOrDefault(value => value != null);
        }

        /// <summary>
        /// Gets the <see cref="double"/> value of the setting with the specified keys.
        /// Will try keys in order until a match is found.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to search for keys.</param>
        /// <param name="keys">The keys that identify the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public static double? GetDouble(this IConfigurationSource source, IEnumerable<string> keys)
        {
            return keys.Select(source.GetDouble)
                       .FirstOrDefault(value => value != null);
        }
    }
}
