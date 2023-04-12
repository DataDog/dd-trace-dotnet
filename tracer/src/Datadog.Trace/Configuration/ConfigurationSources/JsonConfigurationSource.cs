// <copyright file="JsonConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided JSON string.
    /// </summary>
    public class JsonConfigurationSource : IConfigurationSource, ITelemeteredConfigurationSource
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(JsonConfigurationSource));
        private readonly JObject? _configuration;
        private readonly ConfigurationOrigins _origin;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConfigurationSource"/>
        /// class with the specified JSON string.
        /// </summary>
        /// <param name="json">A JSON string that contains configuration values.</param>
        public JsonConfigurationSource(string json)
            : this(json, ConfigurationOrigins.Code)
        {
        }

        internal JsonConfigurationSource(string json, ConfigurationOrigins origin)
        {
            if (json is null) { ThrowHelper.ThrowArgumentNullException(nameof(json)); }

            _configuration = (JObject?)JsonConvert.DeserializeObject(json);
            _origin = origin;
        }

        /// <summary>
        /// Creates a new <see cref="JsonConfigurationSource"/> instance
        /// by loading the JSON string from the specified file.
        /// </summary>
        /// <param name="filename">A JSON file that contains configuration values.</param>
        /// <returns>The newly created configuration source.</returns>
        public static JsonConfigurationSource FromFile(string filename)
            => FromFile(filename, ConfigurationOrigins.Code);

        internal static JsonConfigurationSource FromFile(string filename, ConfigurationOrigins origin)
        {
            var json = File.ReadAllText(filename);
            return new JsonConfigurationSource(json, origin);
        }

        /// <summary>
        /// Gets the <see cref="string"/> value of
        /// the setting with the specified key.
        /// Supports JPath.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        string? IConfigurationSource.GetString(string key)
        {
            return GetValue<string>(key);
        }

        /// <summary>
        /// Gets the <see cref="int"/> value of
        /// the setting with the specified key.
        /// Supports JPath.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        int? IConfigurationSource.GetInt32(string key)
        {
            return GetValue<int?>(key);
        }

        /// <summary>
        /// Gets the <see cref="double"/> value of
        /// the setting with the specified key.
        /// Supports JPath.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        double? IConfigurationSource.GetDouble(string key)
        {
            return GetValue<double?>(key);
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value of
        /// the setting with the specified key.
        /// Supports JPath.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        bool? IConfigurationSource.GetBool(string key)
        {
            return GetValue<bool?>(key);
        }

        /// <summary>
        /// Gets the value of the setting with the specified key and converts it into type <typeparamref name="T"/>.
        /// Supports JPath.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value into.</typeparam>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or the default value of T if not found.</returns>
        public T? GetValue<T>(string key)
        {
            JToken? token = _configuration?.SelectToken(key, errorWhenNoMatch: false);

            return token == null
                       ? default
                       : token.Value<T>();
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the values.
        /// </summary>
        /// <remarks>
        /// Example JSON where `globalTags` is the configuration key.
        /// {
        ///  "globalTags": {
        ///     "name1": "value1",
        ///     "name2": "value2"
        ///     }
        /// }
        /// </remarks>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        /// <exception cref="JsonReaderException">Thrown if the configuration value is not a valid JSON string.</exception>
        public IDictionary<string, string>? GetDictionary(string key)
        {
            return GetDictionaryInternal(key, allowOptionalMappings: false);
        }

        /// <summary>
        /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing all of the values.
        /// </summary>
        /// <remarks>
        /// Example JSON where `globalTags` is the configuration key.
        /// {
        ///  "globalTags": {
        ///     "name1": "value1",
        ///     "name2": "value2"
        ///     }
        /// }
        /// </remarks>
        /// <param name="key">The key that identifies the setting.</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping. This only applies to string values, not JSON objects</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> containing all of the key-value pairs.</returns>
        /// <exception cref="JsonReaderException">Thrown if the configuration value is not a valid JSON string.</exception>
        public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings)
        {
            return GetDictionaryInternal(key, allowOptionalMappings);
        }

        private IDictionary<string, string>? GetDictionaryInternal(string key, bool allowOptionalMappings)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Object)
            {
                try
                {
                    var dictionary = token
                        ?.ToObject<ConcurrentDictionary<string, string>>();
                    return dictionary;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Unable to parse configuration value for {ConfigurationKey} as key-value pairs of strings.", key);
                    return null;
                }
            }

            return StringConfigurationSource.ParseCustomKeyValues(token.ToString(), allowOptionalMappings);
        }

        /// <inheritdoc />
        ConfigurationResult<string>? ITelemeteredConfigurationSource.GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);

            try
            {
                var value = token?.Value<string>();
                if (value is not null)
                {
                    if (validator is null || validator(value))
                    {
                        telemetry.Record(key, value, recordValue, _origin);
                        return ConfigurationResult<string>.Valid(value);
                    }

                    telemetry.Record(key, value, recordValue, _origin, ConfigurationTelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<string>.Invalid(value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue, _origin, ConfigurationTelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<int>? ITelemeteredConfigurationSource.GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);

            try
            {
                var value = token?.Value<int?>();
                if (value.HasValue)
                {
                    if (validator is null || validator(value.Value))
                    {
                        telemetry.Record(key, value.Value, _origin);
                        return ConfigurationResult<int>.Valid(value.Value);
                    }

                    telemetry.Record(key, value.Value, _origin, ConfigurationTelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<int>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, _origin, ConfigurationTelemetryErrorCode.JsonInt32Error);
                throw; // Exising behaviour
            }

            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<double>? ITelemeteredConfigurationSource.GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);

            try
            {
                var value = token?.Value<double?>();
                if (value.HasValue)
                {
                    if (validator is null || validator(value.Value))
                    {
                        telemetry.Record(key, value.Value, _origin);
                        return ConfigurationResult<double>.Valid(value.Value);
                    }

                    telemetry.Record(key, value.Value, _origin, ConfigurationTelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<double>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, _origin, ConfigurationTelemetryErrorCode.JsonDoubleError);
                throw; // Exising behaviour
            }

            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<bool>? ITelemeteredConfigurationSource.GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);

            try
            {
                var value = token?.Value<bool?>();
                if (value.HasValue)
                {
                    if (validator is null || validator(value.Value))
                    {
                        telemetry.Record(key, value.Value, _origin);
                        return ConfigurationResult<bool>.Valid(value.Value);
                    }

                    telemetry.Record(key, value.Value, _origin, ConfigurationTelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<bool>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, _origin, ConfigurationTelemetryErrorCode.JsonBooleanError);
                throw; // Exising behaviour
            }

            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<T>? ITelemeteredConfigurationSource.GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);

            try
            {
                var valueAsString = token?.Value<string>();
                if (valueAsString is not null)
                {
                    var value = converter(valueAsString);
                    if (value.IsValid)
                    {
                        if (validator is null || validator(value.Result))
                        {
                            telemetry.Record(key, valueAsString, recordValue, _origin);
                            return ConfigurationResult<T>.Valid(value.Result);
                        }

                        telemetry.Record(key, valueAsString, recordValue, _origin, ConfigurationTelemetryErrorCode.FailedValidation);
                        return ConfigurationResult<T>.Invalid(value.Result);
                    }

                    telemetry.Record(key, valueAsString, recordValue, _origin, ConfigurationTelemetryErrorCode.ParsingCustomError);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue, _origin, ConfigurationTelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            return null;
        }

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings: false);

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings);

        private ConfigurationResult<IDictionary<string, string>>? GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings)
        {
            var token = _configuration?.SelectToken(key, errorWhenNoMatch: false);
            if (token == null)
            {
                return null;
            }

            var tokenAsString = token.ToString();

            try
            {
                if (token.Type == JTokenType.Object)
                {
                    try
                    {
                        var dictionary = token.ToObject<ConcurrentDictionary<string, string>>();
                        if (dictionary is null)
                        {
                            return null;
                        }

                        return Validate(dictionary);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Unable to parse configuration value for {ConfigurationKey} as key-value pairs of strings.", key);
                        telemetry.Record(key, tokenAsString, recordValue: true, _origin, ConfigurationTelemetryErrorCode.JsonStringError);
                        return null;
                    }
                }

                var result = StringConfigurationSource.ParseCustomKeyValues(tokenAsString, allowOptionalMappings);
                return Validate(result);
            }
            catch (InvalidCastException)
            {
                telemetry.Record(key, tokenAsString, recordValue: true, _origin, ConfigurationTelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            ConfigurationResult<IDictionary<string, string>>? Validate(IDictionary<string, string> dictionary)
            {
                if (validator is null || validator(dictionary))
                {
                    telemetry.Record(key, tokenAsString, recordValue: true, _origin);
                    return ConfigurationResult<IDictionary<string, string>>.Valid(dictionary);
                }

                telemetry.Record(key, tokenAsString, recordValue: true, _origin, ConfigurationTelemetryErrorCode.FailedValidation);
                return ConfigurationResult<IDictionary<string, string>>.Invalid(dictionary);
            }
        }
    }
}
