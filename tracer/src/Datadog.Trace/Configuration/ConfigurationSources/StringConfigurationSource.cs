// <copyright file="StringConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A base <see cref="IConfigurationSource"/> implementation
    /// for string-only configuration sources.
    /// </summary>
    internal abstract class StringConfigurationSource : IConfigurationSource
    {
        private static readonly char[] DictionarySeparatorChars = { ',' };

        public abstract ConfigurationOrigins Origin { get; }

        /// <summary>
        /// Returns a <see cref="IDictionary{TKey, TValue}"/> from parsing
        /// <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A string containing key-value pairs which are comma-separated, and for which the key and value are colon-separated.</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> of key value pairs.</returns>
        [return: NotNullIfNotNull(nameof(data))]
        public static IDictionary<string, string>? ParseCustomKeyValues(string? data, bool allowOptionalMappings)
            => ParseCustomKeyValues(data, allowOptionalMappings, ':');

        [return: NotNullIfNotNull(nameof(data))]
        public static IDictionary<string, string>? ParseCustomKeyValues(string? data, bool allowOptionalMappings, char separator)
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

            bool enableHeaderTagsBehaviors = allowOptionalMappings;
            var entries = data.Split(DictionarySeparatorChars, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                // we need Trim() before looking forthe separator so we can skip entries with no key
                // (that is, entries with a leading separator, like "<empty or whitespace>:value")
                var trimmedEntry = entry.Trim();
                if (trimmedEntry.Length == 0 || trimmedEntry[0] == separator)
                {
                    continue;
                }
                else if (enableHeaderTagsBehaviors && trimmedEntry[trimmedEntry.Length - 1] == separator)
                {
                    // When parsing header tags, any input trailing colons is invalid
                    continue;
                }

                var separatorIndex = enableHeaderTagsBehaviors switch
                {
                    false => trimmedEntry.IndexOf(separator), // In the general case, we split on the first colon
                    true => trimmedEntry.LastIndexOf(separator), // However, for header tags parsing is recommended to split on last colon
                };

                if (separatorIndex < 0 && enableHeaderTagsBehaviors)
                {
                    // entries with no separator are allowed (e.g. key1 and key3 in "key1, key2:value2, key3"),
                    // it's a key with no value.
                    var key = trimmedEntry;
                    dictionary[key] = string.Empty;
                }
                else if (separatorIndex > 0)
                {
                    // if a separator is present with no value, we take the value to be empty (e.g. "key1:, key2: ").
                    // note we already did Trim() on the entire entry, so the key portion only needs TrimEnd().
                    var key = trimmedEntry.Substring(0, separatorIndex).TrimEnd();
                    var value = trimmedEntry.Substring(separatorIndex + 1).Trim();
                    dictionary[key] = value;
                }
            }

            return dictionary;
        }

        protected abstract string? GetString(string key);

        /// <inheritdoc />
        public ConfigurationResult<string> GetString(string key, Func<string, bool>? validator, bool recordValue)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<string>.NotFound();
            }

            if (validator is null || validator(value))
            {
                return ConfigurationResult<string>.Valid(value);
            }

            return ConfigurationResult<string>.Invalid(value);
        }

        /// <inheritdoc />
        public ConfigurationResult<int> GetInt32(string key, Func<int, bool>? validator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<int>.NotFound();
            }

            if (int.TryParse(value, out var result))
            {
                if (validator is null || validator(result))
                {
                    return ConfigurationResult<int>.Valid(result);
                }

                return ConfigurationResult<int>.Invalid(result);
            }

            return ConfigurationResult<int>.ParseFailure();
        }

        /// <inheritdoc />
        public ConfigurationResult<double> GetDouble(string key, Func<double, bool>? validator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<double>.NotFound();
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                if (validator is null || validator(result))
                {
                    return ConfigurationResult<double>.Valid(result);
                }

                return ConfigurationResult<double>.Invalid(result);
            }

            return ConfigurationResult<double>.ParseFailure();
        }

        /// <inheritdoc />
        public ConfigurationResult<bool> GetBool(string key, Func<bool, bool>? validator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<bool>.NotFound();
            }

            var result = value.ToBoolean();
            if (result.HasValue)
            {
                if (validator is null || validator(result.Value))
                {
                    return ConfigurationResult<bool>.Valid(result.Value);
                }

                return ConfigurationResult<bool>.Invalid(result.Value);
            }

            return ConfigurationResult<bool>.ParseFailure();
        }

        /// <inheritdoc />
        public ConfigurationResult<T> GetAs<T>(string key, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<T>.NotFound();
            }

            var result = converter(value);
            if (result.IsValid)
            {
                if (validator is null || validator(result.Result))
                {
                    return ConfigurationResult<T>.Valid(result.Result, value);
                }

                return ConfigurationResult<T>.Invalid(result.Result);
            }

            return ConfigurationResult<T>.ParseFailure();
        }

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, validator, allowOptionalMappings: false, separator: ':');

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<IDictionary<string, string>>.NotFound();
            }

            // We record the original dictionary value here instead of serializing the _parsed_ value
            // Currently we have no validation of the dictionary values during parsing, so there's no way to get
            // a validation error that needs recording at this stage
            var result = ParseCustomKeyValues(value, allowOptionalMappings, separator);

            if (validator is null || validator(result))
            {
                return ConfigurationResult<IDictionary<string, string>>.Valid(result, value);
            }

            return ConfigurationResult<IDictionary<string, string>>.Invalid(result);
        }

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
        {
            var value = GetString(key);

            if (value is null)
            {
                return ConfigurationResult<IDictionary<string, string>>.NotFound();
            }

            // We record the original dictionary value here instead of serializing the _parsed_ value
            // Currently we have no validation of the dictionary values during parsing, so there's no way to get
            // a validation error that needs recording at this stage
            var result = parser(value);

            if (validator is null || validator(result))
            {
                return ConfigurationResult<IDictionary<string, string>>.Valid(result, value);
            }

            return ConfigurationResult<IDictionary<string, string>>.Invalid(result);
        }
    }
}
