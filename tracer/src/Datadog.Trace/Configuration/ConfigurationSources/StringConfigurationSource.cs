// <copyright file="StringConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
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
        private static readonly char[] DictionarySeparatorChars = { ',' };

        /// <summary>
        /// Returns a <see cref="IDictionary{TKey, TValue}"/> from parsing
        /// <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A string containing key-value pairs which are comma-separated, and for which the key and value are colon-separated.</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> of key value pairs.</returns>
        public static IDictionary<string, string>? ParseCustomKeyValues(string? data)
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
        public static IDictionary<string, string>? ParseCustomKeyValues(string? data, bool allowOptionalMappings)
        {
            // A null return value means the key was not present,
            // and CompositeConfigurationSource depends on this behavior
            // (it returns the first non-null value it finds).
            if (data == null)
            {
                return null;
            }

            var dictionary = new ConcurrentDictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data))
            {
                // return empty collection
                return dictionary;
            }

            var entries = data.Split(DictionarySeparatorChars, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                // we need TrimStart() before looking for ':' so we can skip entries with no key
                // (that is, entries with a leading ':', like "<empty or whitespace>:value")
                var trimmedEntry = entry.TrimStart();

                if (trimmedEntry.Length > 0)
                {
                    // colonIndex == 0 is a leading colon, not valid
                    var colonIndex = trimmedEntry.IndexOf(':');

                    if (colonIndex < 0 && allowOptionalMappings)
                    {
                        // entries with no colon are allowed (e.g. "key1, key2:value2, key3"),
                        // it's a key with no value.
                        // note we already did TrimStart(), so we only need TrimEnd().
                        var key = trimmedEntry.TrimEnd();
                        dictionary[key] = string.Empty;
                    }
                    else if (colonIndex > 0)
                    {
                        // split at the first colon only. any other colons are part of the value.
                        // if a colon is present with no value, we take the value to be empty (e.g. "key1:, key2: ").
                        // note we already did TrimStart() on the key, so it only needs TrimEnd().
                        var key = trimmedEntry.Substring(0, colonIndex).TrimEnd();
                        var value = trimmedEntry.Substring(colonIndex + 1).Trim();
                        dictionary[key] = value;
                    }
                }
            }

            return dictionary;
        }

        /// <inheritdoc />
        public abstract string? GetString(string key);

        /// <inheritdoc />
        public virtual int? GetInt32(string key)
        {
            var value = GetString(key);

            return value is not null
                && int.TryParse(value, out var result)
                       ? result
                       : null;
        }

        /// <inheritdoc />
        public double? GetDouble(string key)
        {
            var value = GetString(key);

            return value is not null && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                       ? result
                       : null;
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
        public IDictionary<string, string>? GetDictionary(string key)
        {
            return ParseCustomKeyValues(GetString(key), allowOptionalMappings: false);
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings)
        {
            return ParseCustomKeyValues(GetString(key), allowOptionalMappings);
        }
    }
}
