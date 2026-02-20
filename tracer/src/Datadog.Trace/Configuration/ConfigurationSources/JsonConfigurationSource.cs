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
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided JSON string.
    /// </summary>
    internal class JsonConfigurationSource : IConfigurationSource
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(JsonConfigurationSource));
        private readonly JToken? _configuration;

        internal JsonConfigurationSource(string json, ConfigurationOrigins origin)
            : this(json, origin, j => (JToken?)JsonHelper.DeserializeObject(j))
        {
        }

        internal JsonConfigurationSource(string json, ConfigurationOrigins origin, string? filename)
            : this(json, origin, j => (JToken?)JsonHelper.DeserializeObject(j))
        {
            JsonConfigurationFilePath = filename;
        }

        internal JsonConfigurationSource(JToken? configToken, ConfigurationOrigins origin, Func<JToken?, JToken?> extractConfig)
        {
            if (configToken is null) { ThrowHelper.ThrowArgumentNullException(nameof(configToken)); }

            if (extractConfig is null) { ThrowHelper.ThrowArgumentNullException(nameof(extractConfig)); }

            _configuration = extractConfig(configToken);
            Origin = origin;
        }

        private protected JsonConfigurationSource(string json, ConfigurationOrigins origin, Func<string, JToken?> deserialize)
        {
            if (json is null) { ThrowHelper.ThrowArgumentNullException(nameof(json)); }

            if (deserialize is null) { ThrowHelper.ThrowArgumentNullException(nameof(deserialize)); }

            _configuration = deserialize(json);
            Origin = origin;
        }

        public ConfigurationOrigins Origin { get; }

        internal string? JsonConfigurationFilePath { get; }

        internal bool TreatNullDictionaryAsEmpty { get; set; } = true;

        /// <summary>
        /// Creates a new <see cref="JsonConfigurationSource"/> instance
        /// by loading the JSON string from the specified file.
        /// </summary>
        /// <param name="filename">A JSON file that contains configuration values.</param>
        /// <param name="origin">The origin to use for telemetry.</param>
        /// <returns>The newly created configuration source.</returns>
        internal static JsonConfigurationSource FromFile(string filename, ConfigurationOrigins origin)
        {
            var json = File.ReadAllText(filename);
            return new JsonConfigurationSource(json, origin, filename);
        }

        /// <summary>
        /// Gets the value of the setting with the specified key and converts it into type <typeparamref name="T"/>.
        /// Supports JPath.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value into.</typeparam>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or the default value of T if not found.</returns>
        internal T? GetValueInternal<T>(string key)
        {
            JToken? token = SelectToken(key);

            return token == null
                       ? default
                       : token.Value<T>();
        }

        /// <inheritdoc />
        public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        {
            var token = SelectToken(key);

            try
            {
                var value = token?.Value<string>();
                if (value is not null)
                {
                    if (validator is null || validator(value))
                    {
                        telemetry.Record(key, value, recordValue, Origin);
                        return ConfigurationResult<string>.Valid(value);
                    }

                    telemetry.Record(key, value, recordValue, Origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<string>.Invalid(value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue, Origin, TelemetryErrorCode.JsonStringError);
                throw; // Existing behaviour
            }

            return ConfigurationResult<string>.NotFound();
        }

        /// <inheritdoc />
        public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        {
            var token = SelectToken(key);

            try
            {
                var value = token?.Value<int?>();
                if (value.HasValue)
                {
                    if (validator is null || validator(value.Value))
                    {
                        telemetry.Record(key, value.Value, Origin);
                        return ConfigurationResult<int>.Valid(value.Value);
                    }

                    telemetry.Record(key, value.Value, Origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<int>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, Origin, TelemetryErrorCode.JsonInt32Error);
                throw; // Exising behaviour
            }

            return ConfigurationResult<int>.NotFound();
        }

        /// <inheritdoc />
        public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        {
            var token = SelectToken(key);

            try
            {
                var value = token?.Value<double?>();
                if (value.HasValue)
                {
                    if (validator is null || validator(value.Value))
                    {
                        telemetry.Record(key, value.Value, Origin);
                        return ConfigurationResult<double>.Valid(value.Value);
                    }

                    telemetry.Record(key, value.Value, Origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<double>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, Origin, TelemetryErrorCode.JsonDoubleError);
                throw; // Exising behaviour
            }

            return ConfigurationResult<double>.NotFound();
        }

        /// <inheritdoc />
        public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        {
            var token = SelectToken(key);

            try
            {
                var value = token?.Value<bool?>();
                if (value.HasValue)
                {
                    if (validator is null || validator(value.Value))
                    {
                        telemetry.Record(key, value.Value, Origin);
                        return ConfigurationResult<bool>.Valid(value.Value);
                    }

                    telemetry.Record(key, value.Value, Origin, TelemetryErrorCode.FailedValidation);
                    return ConfigurationResult<bool>.Invalid(value.Value);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue: true, Origin, TelemetryErrorCode.JsonBooleanError);
                throw; // Exising behaviour
            }

            return ConfigurationResult<bool>.NotFound();
        }

        /// <inheritdoc />
        public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
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
                            telemetry.Record(key, valueAsString, recordValue, Origin);
                            return ConfigurationResult<T>.Valid(value.Result, valueAsString);
                        }

                        telemetry.Record(key, valueAsString, recordValue, Origin, TelemetryErrorCode.FailedValidation);
                        return ConfigurationResult<T>.Invalid(value.Result);
                    }

                    telemetry.Record(key, valueAsString, recordValue, Origin, TelemetryErrorCode.ParsingCustomError);
                }
            }
            catch (Exception)
            {
                telemetry.Record(key, token?.ToString(), recordValue, Origin, TelemetryErrorCode.JsonStringError);
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
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, telemetry, validator, allowOptionalMappings: false, separator: ':');

        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
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
                        telemetry.Record(key, tokenAsString, recordValue: true, Origin, TelemetryErrorCode.JsonStringError);
                        return ConfigurationResult<IDictionary<string, string>>.ParseFailure();
                    }
                }

                var result = StringConfigurationSource.ParseCustomKeyValues(tokenAsString, allowOptionalMappings, separator);
                return Validate(result);
            }
            catch (InvalidCastException)
            {
                telemetry.Record(key, tokenAsString, recordValue: true, Origin, TelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            ConfigurationResult<IDictionary<string, string>> Validate(IDictionary<string, string> dictionary)
            {
                if (validator is null || validator(dictionary))
                {
                    telemetry.Record(key, tokenAsString, recordValue: true, Origin);
                    return ConfigurationResult<IDictionary<string, string>>.Valid(dictionary, tokenAsString);
                }

                telemetry.Record(key, tokenAsString, recordValue: true, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<IDictionary<string, string>>.Invalid(dictionary);
            }
        }

        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
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
                        telemetry.Record(key, tokenAsString, recordValue: true, Origin, TelemetryErrorCode.JsonStringError);
                        return ConfigurationResult<IDictionary<string, string>>.ParseFailure();
                    }
                }

                var result = parser(tokenAsString);
                return Validate(result);
            }
            catch (InvalidCastException)
            {
                telemetry.Record(key, tokenAsString, recordValue: true, Origin, TelemetryErrorCode.JsonStringError);
                throw; // Exising behaviour
            }

            ConfigurationResult<IDictionary<string, string>> Validate(IDictionary<string, string> dictionary)
            {
                if (validator is null || validator(dictionary))
                {
                    telemetry.Record(key, tokenAsString, recordValue: true, Origin);
                    return ConfigurationResult<IDictionary<string, string>>.Valid(dictionary, tokenAsString);
                }

                telemetry.Record(key, tokenAsString, recordValue: true, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<IDictionary<string, string>>.Invalid(dictionary);
            }
        }

        private protected virtual JToken? SelectToken(string key) => _configuration?.SelectToken(key, errorWhenNoMatch: false);

        private protected virtual IDictionary<string, string>? ConvertToDictionary(string key, JToken token)
        {
            return token.ToObject<ConcurrentDictionary<string, string>>();
        }
    }
}
