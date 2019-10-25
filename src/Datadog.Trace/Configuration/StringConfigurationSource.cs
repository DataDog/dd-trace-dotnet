using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A base <see cref="IConfigurationSource"/> implementation
    /// for string-only configuration sources.
    /// </summary>
    public abstract class StringConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// Returns a <see cref="IDictionary{TKey, TValue}"/> from parsing
        /// <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A string containing key-value pairs which are comma-separated, and for which the key and value are colon-separated.</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> of key value pairs.</returns>
        public static IDictionary<string, string> ParseCustomKeyValues(string data)
        {
            var dictionary = new ConcurrentDictionary<string, string>();

            if (data == null)
            {
                return dictionary;
            }

            var entries = data.Split(',');

            foreach (var e in entries)
            {
                var kv = e.Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }

                var key = kv[0];
                var value = kv[1];
                dictionary[key] = value;
            }

            return dictionary;
        }

        /// <inheritdoc />
        public abstract string GetString(string key);

        /// <inheritdoc />
        public virtual int? GetInt32(string key)
        {
            string value = GetString(key);

            return int.TryParse(value, out int result)
                       ? result
                       : (int?)null;
        }

        /// <inheritdoc />
        public double? GetDouble(string key)
        {
            string value = GetString(key);

            return double.TryParse(value, out double result)
                       ? result
                       : (double?)null;
        }

        /// <inheritdoc />
        public virtual bool? GetBool(string key)
        {
            string value = GetString(key);

            return bool.TryParse(value, out bool result)
                       ? result
                       : (bool?)null;
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        public IDictionary<string, string> GetDictionary(string key)
        {
            return ParseCustomKeyValues(GetString(key));
        }
    }
}
