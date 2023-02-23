// <copyright file="CompositeConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents one or more configuration sources.
    /// </summary>
    public class CompositeConfigurationSource : IConfigurationSource, IEnumerable<IConfigurationSource>
    {
        private readonly List<IConfigurationSource> _sources = new();

        /// <summary>
        /// Adds a new configuration source to this instance.
        /// </summary>
        /// <param name="source">The configuration source to add.</param>
        public void Add(IConfigurationSource source)
        {
            if (source == null) { ThrowHelper.ThrowArgumentNullException(nameof(source)); }

            _sources.Add(source);
        }

        /// <summary>
        /// Inserts an element into the <see cref="CompositeConfigurationSource"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The configuration source to insert.</param>
        public void Insert(int index, IConfigurationSource item)
        {
            if (item == null) { ThrowHelper.ThrowArgumentNullException(nameof(item)); }

            _sources.Insert(index, item);
        }

        /// <summary>
        /// Gets the <see cref="string"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public string GetString(string key)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].GetString(key) is { } value)
                {
                    return value;
                }
            }

            return default;
        }

        /// <summary>
        /// Gets the <see cref="int"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public int? GetInt32(string key)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].GetInt32(key) is { } value)
                {
                    return value;
                }
            }

            return default;
        }

        /// <summary>
        /// Gets the <see cref="double"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public double? GetDouble(string key)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].GetDouble(key) is { } value)
                {
                    return value;
                }
            }

            return default;
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        public bool? GetBool(string key)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].GetBool(key) is { } value)
                {
                    return value;
                }
            }

            return default;
        }

        /// <summary>Returns an enumerator that iterates through the <see cref="T:System.Collections.Generic.List`1" />.</summary>
        /// <returns>A <see cref="T:System.Collections.Generic.List`1.Enumerator" /> for the <see cref="T:System.Collections.Generic.List`1" />.</returns>
        internal List<IConfigurationSource>.Enumerator GetEnumerator()
        {
            return _sources.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator<IConfigurationSource> IEnumerable<IConfigurationSource>.GetEnumerator()
        {
            return _sources.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _sources.GetEnumerator();
        }

        /// <inheritdoc />
        public IDictionary<string, string> GetDictionary(string key)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].GetDictionary(key) is { } value)
                {
                    return value;
                }
            }

            return default;
        }

        /// <inheritdoc />
        public IDictionary<string, string> GetDictionary(string key, bool allowOptionalMappings)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].GetDictionary(key, allowOptionalMappings) is { } value)
                {
                    return value;
                }
            }

            return default;
        }
    }
}
