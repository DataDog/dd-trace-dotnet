using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided JSON string.
    /// </summary>
    public class JsonConfigurationSource : IConfigurationSource
    {
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
        /// Gets a Dictionary containing all of the configuration values where keys and values are strings.
        /// </summary>
        /// <returns>Dictionary of key value strings</returns>
        public Dictionary<string, string> GetAllEntries()
        {
            var allEntries = new Dictionary<string, string>();
            var enumerator = _configuration.GetEnumerator();

            while (!enumerator.MoveNext())
            {
                allEntries.Add(enumerator.Current.Key, enumerator.Current.Value.ToString());
            }

            return allEntries;
        }
    }
}
