// <copyright file="JsonConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided JSON string.
    /// </summary>
    public class JsonConfigurationSource : IConfigurationSource
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(JsonConfigurationSource));

        private readonly JObject _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConfigurationSource"/>
        /// class with the specified JSON string.
        /// </summary>
        /// <param name="json">A JSON string that contains configuration values.</param>
        public JsonConfigurationSource(string json)
        {
            _configuration = (JObject)JsonConvert.DeserializeObject(json);
        }

        /// <summary>
        /// Creates a new <see cref="JsonConfigurationSource"/> instance
        /// by loading the JSON string from the specified file.
        /// </summary>
        /// <param name="filename">A JSON file that contains configuration values.</param>
        /// <returns>The newly created configuration source.</returns>
        public static JsonConfigurationSource FromFile(string filename)
        {
            string json = File.ReadAllText(filename);
            return new JsonConfigurationSource(json);
        }

        /// <summary>
        /// Gets the <see cref="string"/> value of
        /// the setting with the specified key.
        /// Supports JPath.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        string IConfigurationSource.GetString(string key)
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
        public T GetValue<T>(string key)
        {
            JToken token = _configuration.SelectToken(key, errorWhenNoMatch: false);

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
        public IDictionary<string, string> GetDictionary(string key)
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
        public IDictionary<string, string> GetDictionary(string key, bool allowOptionalMappings)
        {
            return GetDictionaryInternal(key, allowOptionalMappings);
        }

        private IDictionary<string, string> GetDictionaryInternal(string key, bool allowOptionalMappings)
        {
            var token = _configuration.SelectToken(key, errorWhenNoMatch: false);
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
    }
}
