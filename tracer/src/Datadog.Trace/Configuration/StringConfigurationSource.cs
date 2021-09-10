// <copyright file="StringConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.ExtensionMethods;

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
            return ParseCustomKeyValues(data, allowOptionalMappings: false);
        }

        /// <summary>
        /// Returns a <see cref="IDictionary{TKey, TValue}"/> from parsing
        /// <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A string containing key-value pairs which are comma-separated, and for which the key and value are colon-separated.</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> of key value pairs.</returns>
        public static IDictionary<string, string> ParseCustomKeyValues(string data, bool allowOptionalMappings)
        {
            var dictionary = new ConcurrentDictionary<string, string>();

            // A null return value means the key was not present,
            // and CompositeConfigurationSource depends on this behavior
            // (it returns the first non-null value it finds).
            if (data == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(data))
            {
                return dictionary;
            }

            var entries = data.Split(',');

            foreach (var e in entries)
            {
                var kv = e.Split(':');
                if (allowOptionalMappings && kv.Length == 1)
                {
                    var key = kv[0];
                    var value = string.Empty;
                    dictionary[key] = value;
                }
                else if (kv.Length != 2)
                {
                    continue;
                }
                else
                {
                    var key = kv[0];
                    var value = kv[1];
                    dictionary[key] = value;
                }
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

            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result)
                       ? result
                       : (double?)null;
        }

        /// <inheritdoc />
        public virtual bool? GetBool(string key)
        {
            var value = GetString(key);
            return value?.ToBoolean();
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        public IDictionary<string, string> GetDictionary(string key)
        {
            return ParseCustomKeyValues(GetString(key), allowOptionalMappings: false);
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        public IDictionary<string, string> GetDictionary(string key, bool allowOptionalMappings)
        {
            return ParseCustomKeyValues(GetString(key), allowOptionalMappings);
        }
    }
}
