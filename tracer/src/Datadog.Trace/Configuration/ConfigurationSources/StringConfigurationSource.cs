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
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A base <see cref="IConfigurationSource"/> implementation
    /// for string-only configuration sources.
    /// </summary>
    public abstract class StringConfigurationSource : IConfigurationSource, ITelemeteredConfigurationSource
    {
        private static readonly char[] DictionarySeparatorChars = { ',' };

        internal abstract ConfigurationOrigins Origin { get; }

        /// <summary>
        /// Returns a <see cref="IDictionary{TKey, TValue}"/> from parsing
        /// <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A string containing key-value pairs which are comma-separated, and for which the key and value are colon-separated.</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> of key value pairs.</returns>
        [PublicApi]
        public static IDictionary<string, string>? ParseCustomKeyValues(string? data)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.StringConfigurationSource_ParseCustomKeyValues);
            return ParseCustomKeyValuesInternal(data, allowOptionalMappings: false);
        }

        /// <summary>
        /// Returns a <see cref="IDictionary{TKey, TValue}"/> from parsing
        /// <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A string containing key-value pairs which are comma-separated, and for which the key and value are colon-separated.</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> of key value pairs.</returns>
        [PublicApi]
        [return: NotNullIfNotNull(nameof(data))]
        public static IDictionary<string, string>? ParseCustomKeyValues(string? data, bool allowOptionalMappings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.StringConfigurationSource_ParseCustomKeyValues_AllowOptionalMappings);
            return ParseCustomKeyValuesInternal(data, allowOptionalMappings);
        }

        [return: NotNullIfNotNull(nameof(data))]
        internal static unsafe IDictionary<string, string>? ParseCustomKeyValuesInternal(string? data, bool allowOptionalMappings)
        {
            return ParseCustomKeyValuesInternal(data, allowOptionalMappings, &Selector);
            static bool Selector(ref string key, ref string value) => true;
        }

        [return: NotNullIfNotNull(nameof(data))]
        internal static unsafe IDictionary<string, string>? ParseCustomKeyValuesInternal(string? data, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
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
                        var value = string.Empty;
                        if (selector(ref key, ref value))
                        {
                            dictionary[key] = value;
                        }
                    }
                    else if (colonIndex > 0)
                    {
                        // split at the first colon only. any other colons are part of the value.
                        // if a colon is present with no value, we take the value to be empty (e.g. "key1:, key2: ").
                        // note we already did TrimStart() on the key, so it only needs TrimEnd().
                        var key = trimmedEntry.Substring(0, colonIndex).TrimEnd();
                        var value = trimmedEntry.Substring(colonIndex + 1).Trim();
                        if (selector(ref key, ref value))
                        {
                            dictionary[key] = value;
                        }
                    }
                }
            }

            return dictionary;
        }

        /// <inheritdoc />
        public abstract string? GetString(string key);

        /// <inheritdoc />
        [PublicApi]
        public virtual int? GetInt32(string key)
        {
            var value = GetString(key);

            return value is not null
                && int.TryParse(value, out var result)
                       ? result
                       : null;
        }

        /// <inheritdoc />
        [PublicApi]
        public double? GetDouble(string key)
        {
            var value = GetString(key);

            return value is not null && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                       ? result
                       : null;
        }

        /// <inheritdoc />
        [PublicApi]
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
        [PublicApi]
        public IDictionary<string, string>? GetDictionary(string key)
        {
            return ParseCustomKeyValuesInternal(GetString(key), allowOptionalMappings: false);
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        [PublicApi]
        public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings)
        {
            return ParseCustomKeyValuesInternal(GetString(key), allowOptionalMappings);
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="selector">Selector for dictionary</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        [PublicApi]
        public unsafe IDictionary<string, string>? GetDictionary(string key, delegate*<ref string, ref string, bool> selector)
        {
            return ParseCustomKeyValuesInternal(GetString(key), allowOptionalMappings: false, selector: selector);
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> from parsing
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <param name="selector">Selector for dictionary</param>
        /// <returns><see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        [PublicApi]
        public unsafe IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
        {
            return ParseCustomKeyValuesInternal(GetString(key), allowOptionalMappings, selector: selector);
        }

        /// <inheritdoc />
        ConfigurationResult<string>? ITelemeteredConfigurationSource.GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        {
            var value = GetString(key);

            if (value is null)
            {
                return null;
            }

            if (validator is null || validator(value))
            {
                telemetry.Record(key, value, recordValue, Origin);
                return ConfigurationResult<string>.Valid(value);
            }

            telemetry.Record(key, value, recordValue, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<string>.Invalid(value);
        }

        /// <inheritdoc />
        ConfigurationResult<int>? ITelemeteredConfigurationSource.GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return null;
            }

            if (int.TryParse(value, out var result))
            {
                if (validator is null || validator(result))
                {
                    telemetry.Record(key, result, Origin);
                    return ConfigurationResult<int>.Valid(result);
                }

                telemetry.Record(key, result, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<int>.Invalid(result);
            }

            telemetry.Record(key, value, recordValue: true, Origin, TelemetryErrorCode.ParsingInt32Error);
            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<double>? ITelemeteredConfigurationSource.GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return null;
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                if (validator is null || validator(result))
                {
                    telemetry.Record(key, result, Origin);
                    return ConfigurationResult<double>.Valid(result);
                }

                telemetry.Record(key, result, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<double>.Invalid(result);
            }

            telemetry.Record(key, value, recordValue: true, Origin, TelemetryErrorCode.ParsingDoubleError);
            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<bool>? ITelemeteredConfigurationSource.GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        {
            var value = GetString(key);

            if (value is null)
            {
                return null;
            }

            var result = value.ToBoolean();
            if (result.HasValue)
            {
                if (validator is null || validator(result.Value))
                {
                    telemetry.Record(key, result.Value, Origin);
                    return ConfigurationResult<bool>.Valid(result.Value);
                }

                telemetry.Record(key, result.Value, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<bool>.Invalid(result.Value);
            }

            telemetry.Record(key, value, recordValue: true, Origin, TelemetryErrorCode.ParsingBooleanError);
            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<T>? ITelemeteredConfigurationSource.GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        {
            var value = GetString(key);

            if (value is null)
            {
                return null;
            }

            var result = converter(value);
            if (result.IsValid)
            {
                if (validator is null || validator(result.Result))
                {
                    telemetry.Record(key, value, recordValue, Origin);
                    return ConfigurationResult<T>.Valid(result.Result);
                }

                telemetry.Record(key, value, recordValue, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<T>.Invalid(result.Result);
            }

            telemetry.Record(key, value, recordValue, Origin, TelemetryErrorCode.ParsingCustomError);
            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings: false);

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings);

        /// <inheritdoc />
        unsafe ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, delegate*<ref string, ref string, bool> selector)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings: false, selector);

        /// <inheritdoc />
        unsafe ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings, selector);

        private unsafe ConfigurationResult<IDictionary<string, string>>? GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings)
        {
            return GetDictionary(key, telemetry, validator, allowOptionalMappings, &Selector);
            static bool Selector(ref string key, ref string value) => true;
        }

        private unsafe ConfigurationResult<IDictionary<string, string>>? GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
        {
            var value = GetString(key);

            if (value is null)
            {
                return null;
            }

            // We record the original dictionary value here instead of serializing the _parsed_ value
            // Currently we have no validation of the dictionary values during parsing, so there's no way to get
            // a validation error that needs recording at this stage
            var result = ParseCustomKeyValuesInternal(value, allowOptionalMappings, selector);

            if (validator is null || validator(result))
            {
                telemetry.Record(key, value, recordValue: true, Origin);
                return ConfigurationResult<IDictionary<string, string>>.Valid(result);
            }

            telemetry.Record(key, value, recordValue: true, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<IDictionary<string, string>>.Invalid(result);
        }
    }
}
