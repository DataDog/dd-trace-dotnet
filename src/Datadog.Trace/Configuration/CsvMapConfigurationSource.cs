using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves values from
    /// a comma-separate list of key-value pairs.
    /// </summary>
    public class CsvMapConfigurationSource : StringConfigurationSource
    {
        private ConcurrentDictionary<string, string> _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="CsvMapConfigurationSource"/> class
        /// using a custom configuration string format.
        /// </summary>
        /// <param name="config">Configuration string that is a comma-separated list of key, value pairs where key and value are colon-separated.</param>
        /// <example>An example configuration string would be "'globalTag1':'someValue','globalTag2':'anotherValue'"</example>
        public CsvMapConfigurationSource(string config)
        {
            _data = ParseConfig(config);
        }

        /// <summary>
        /// Gets the configuration data as a <see cref="ConcurrentDictionary{TKey, TValue}"/>
        /// </summary>
        public ConcurrentDictionary<string, string> Data
        {
            get { return _data; }
        }

        /// <inheritdoc />
        public override string GetString(string key)
        {
            throw new NotImplementedException();
        }

        private ConcurrentDictionary<string, string> ParseConfig(string config)
        {
            ConcurrentDictionary<string, string> data = new ConcurrentDictionary<string, string>();

            var entries = config.Split(',');
            foreach (var e in entries)
            {
                var kv = e.Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }

                var key = kv[0];
                var value = kv[1];
                data[key] = value;
            }

            return data;
        }
    }
}
