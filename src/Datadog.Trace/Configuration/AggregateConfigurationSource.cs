using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents one or more configuration sources.
    /// </summary>
    public class AggregateConfigurationSource : IConfigurationSource
    {
        private readonly List<IConfigurationSource> _sources = new List<IConfigurationSource>();

        /// <summary>
        /// Adds a new configuration source to this instance.
        /// </summary>
        /// <param name="source">The configuration source to add.</param>
        public void AddSource(IConfigurationSource source)
        {
            _sources.Add(source);
        }

        /// <summary>
        /// Gets the <see cref="string"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        public string GetString(string key)
        {
            return _sources.Select(source => source.GetString(key))
                           .FirstOrDefault(value => value != null);
        }

        /// <summary>
        /// Gets the <see cref="int"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        public int? GetInt32(string key)
        {
            return _sources.Select(source => source.GetInt32(key))
                           .FirstOrDefault(value => value != null);
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or null if not found.</returns>
        public bool? GetBool(string key)
        {
            return _sources.Select(source => source.GetBool(key))
                           .FirstOrDefault(value => value != null);
        }
    }
}
