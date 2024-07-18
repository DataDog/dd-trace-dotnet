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
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
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
        private readonly JToken? _configuration;
        private readonly ConfigurationOrigins _origin;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConfigurationSource"/>
        /// class with the specified JSON string.
        /// </summary>
        /// <param name="json">A JSON string that contains configuration values.</param>
        [PublicApi]
        public JsonConfigurationSource(string json)
            : this(json, ConfigurationOrigins.Code)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.JsonConfigurationSource_Ctor_Json);
        }

        internal JsonConfigurationSource(string json, ConfigurationOrigins origin)
            : this(json, origin, j => (JToken?)JsonConvert.DeserializeObject(j))
        {
        }

        private protected JsonConfigurationSource(string json, ConfigurationOrigins origin, Func<string, JToken?> deserialize)
        {
            if (json is null) { ThrowHelper.ThrowArgumentNullException(nameof(json)); }

            if (deserialize is null) { ThrowHelper.ThrowArgumentNullException(nameof(deserialize)); }

            _configuration = deserialize(json);
            _origin = origin;
        }

        internal bool TreatNullDictionaryAsEmpty { get; set; } = true;

        /// <summary>
        /// Creates a new <see cref="JsonConfigurationSource"/> instance
        /// by loading the JSON string from the specified file.
        /// </summary>
        /// <param name="filename">A JSON file that contains configuration values.</param>
        /// <returns>The newly created configuration source.</returns>
        [PublicApi]
        public static JsonConfigurationSource FromFile(string filename)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.JsonConfigurationSource_FromFile);
            return FromFile(filename, ConfigurationOrigins.Code);
        }

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
            return GetValueInternal<string>(key);
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
            return GetValueInternal<int?>(key);
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
            return GetValueInternal<double?>(key);
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value of
        /// the setting with the specified key.
        /// Supports JPath.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        [PublicApi]
        bool? IConfigurationSource.GetBool(string key)
        {
            return GetValueInternal<bool?>(key);
        }

        /// <summary>
        /// Gets the value of the setting with the specified key and converts it into type <typeparamref name="T"/>.
        /// Supports JPath.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value into.</typeparam>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or the default value of T if not found.</returns>
        [PublicApi]
        public T? GetValue<T>(string key) => GetValueInternal<T>(key);

        internal T? GetValueInternal<T>(string key)
        {
            JToken? token = SelectToken(key);

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
            var token = SelectToken(key);
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Object)
            {
                try
                {
                    var dictionary = ConvertToDictionary(key, token);
                    return dictionary;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Unable to parse configuration value for {ConfigurationKey} as key-value pairs of strings.", key);
                    return null;
                }
            }

            return StringConfigurationSource.ParseCustomKeyValuesInternal(token.ToString(), allowOptionalMappings);
        }

        /// <inheritdoc />
        bool ITelemeteredConfigurationSource.IsPresent(string key)
        {
            JToken? token = SelectToken(key);

            return token is not null;
        }

        /// <inheritdoc />
        ConfigurationResult<string> ITelemeteredConfigurationSource.GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        {
            var token = SelectToken(key);

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

                    telemetry.Record(key, value, recordValue, _origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<string>.Invalid(value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue, _origin, TelemetryErrorCode.JsonStringError);
                throw; // Existing behaviour
            }

            return ConfigurationResult<string>.NotFound();
        }

        /// <inheritdoc />
        ConfigurationResult<int> ITelemeteredConfigurationSource.GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        {
            var token = SelectToken(key);

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

                    telemetry.Record(key, value.Value, _origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<int>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, _origin, TelemetryErrorCode.JsonInt32Error);
                throw; // Exising behaviour
            }

            return ConfigurationResult<int>.NotFound();
        }

        /// <inheritdoc />
        ConfigurationResult<double> ITelemeteredConfigurationSource.GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        {
            var token = SelectToken(key);

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

                    telemetry.Record(key, value.Value, _origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<double>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, _origin, TelemetryErrorCode.JsonDoubleError);
                throw; // Exising behaviour
            }

            return ConfigurationResult<double>.NotFound();
        }

        /// <inheritdoc />
        ConfigurationResult<bool> ITelemeteredConfigurationSource.GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        {
            var token = SelectToken(key);

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

                    telemetry.Record(key, value.Value, _origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<bool>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, _origin, TelemetryErrorCode.JsonBooleanError);
                throw; // Exising behaviour
            }

            return ConfigurationResult<bool>.NotFound();
        }

        /// <inheritdoc />
        ConfigurationResult<T> ITelemeteredConfigurationSource.GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        {
            var token = SelectToken(key);

            try
            {
                var valueAsString = JTokenToString(token);

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

                        telemetry.Record(key, valueAsString, recordValue, _origin, TelemetryErrorCode.FailedValidation);
                        return ConfigurationResult<T>.Invalid(value.Result);
                    }

                    telemetry.Record(key, valueAsString, recordValue, _origin, TelemetryErrorCode.ParsingCustomError);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue, _origin, TelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            return ConfigurationResult<T>.NotFound();
        }

        internal static string? JTokenToString(JToken? token)
        {
            return token switch
            {
                null => null,
                _ => token.Type switch
                {
                    JTokenType.Null or JTokenType.None or JTokenType.Undefined => null, // handle null-like values
                    JTokenType.String => token.Value<string>(), // return the underlying string value
                    _ => token.ToString(Formatting.None) // serialize back into json
                }
            };
        }

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>> ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings: false, separator: ':');

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>> ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings, separator);

        private protected virtual JToken? SelectToken(string key) => _configuration?.SelectToken(key, errorWhenNoMatch: false);

        private protected virtual IDictionary<string, string>? ConvertToDictionary(string key, JToken token)
        {
            return token.ToObject<ConcurrentDictionary<string, string>>();
        }

        private ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
        {
            var token = SelectToken(key);
            if (token == null)
            {
                return ConfigurationResult<IDictionary<string, string>>.NotFound();
            }

            if (!TreatNullDictionaryAsEmpty && !token.HasValues)
            {
                return ConfigurationResult<IDictionary<string, string>>.NotFound();
            }

            var tokenAsString = token.ToString();

            try
            {
                if (token.Type == JTokenType.Object || token.Type == JTokenType.Array)
                {
                    try
                    {
                        var dictionary = ConvertToDictionary(key, token);
                        if (dictionary is null)
                        {
                            // AFAICT this should never return null in practice - we
                            // already checked the token is not null, and it will throw
                            // if parsing fails, so using parsing failure here for safety
                            return ConfigurationResult<IDictionary<string, string>>.ParseFailure();
                        }

                        return Validate(dictionary);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Unable to parse configuration value for {ConfigurationKey} as key-value pairs of strings.", key);
                        telemetry.Record(key, tokenAsString, recordValue: true, _origin, TelemetryErrorCode.JsonStringError);
                        return ConfigurationResult<IDictionary<string, string>>.ParseFailure();
                    }
                }

                var result = StringConfigurationSource.ParseCustomKeyValuesInternal(tokenAsString, allowOptionalMappings, separator);
                return Validate(result);
            }
            catch (InvalidCastException)
            {
                telemetry.Record(key, tokenAsString, recordValue: true, _origin, TelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            ConfigurationResult<IDictionary<string, string>> Validate(IDictionary<string, string> dictionary)
            {
                if (validator is null || validator(dictionary))
                {
                    telemetry.Record(key, tokenAsString, recordValue: true, _origin);
                    return ConfigurationResult<IDictionary<string, string>>.Valid(dictionary);
                }

                telemetry.Record(key, tokenAsString, recordValue: true, _origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<IDictionary<string, string>>.Invalid(dictionary);
            }
        }
    }
}
