// <copyright file="CompositeConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents one or more configuration sources.
    /// </summary>
    internal sealed class CompositeConfigurationSource : IConfigurationSource, IEnumerable<IConfigurationSource>
    {
        private readonly List<IConfigurationSource> _sources;

        public CompositeConfigurationSource()
        {
            _sources = new();
        }

#if NETCOREAPP3_1_OR_GREATER
        public CompositeConfigurationSource(ReadOnlySpan<IConfigurationSource> sources)
        {
            _sources = [..sources];
        }
#else
        public CompositeConfigurationSource(IEnumerable<IConfigurationSource> sources)
        {
            _sources = [..sources];
        }
#endif

        /// <summary>
        /// Adds a new configuration source to this instance.
        /// </summary>
        /// <param name="source">The configuration source to add.</param>
        public void Add(IConfigurationSource source)
        {
            if (source == null!) { ThrowHelper.ThrowArgumentNullException(nameof(source)); }

            _sources.Add(source);
        }

        /// <inheritdoc />
        IEnumerator<IConfigurationSource> IEnumerable<IConfigurationSource>.GetEnumerator() => _sources.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => _sources.GetEnumerator();

        /// <inheritdoc />
        public ConfigurationResult<string> GetString(string key, Func<string, bool>? validator)
        {
            // Iterate in reverse order and keep the last successful value so the first source has precedence.
            var result = ConfigurationResult<string>.NotFound();
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetString(key, validator);
                if (value.IsValid)
                {
                    result = value;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<int> GetInt32(string key, Func<int, bool>? validator)
        {
            // Iterate in reverse order and keep the last successful value so the first source has precedence.
            var result = ConfigurationResult<int>.NotFound();
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetInt32(key, validator);
                if (value.IsValid)
                {
                    result = value;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<double> GetDouble(string key, Func<double, bool>? validator)
        {
            // Iterate in reverse order and keep the last successful value so the first source has precedence.
            var result = ConfigurationResult<double>.NotFound();
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetDouble(key, validator);
                if (value.IsValid)
                {
                    result = value;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<bool> GetBool(string key, Func<bool, bool>? validator)
        {
            // Iterate in reverse order and keep the last successful value so the first source has precedence.
            var result = ConfigurationResult<bool>.NotFound();
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetBool(key, validator);
                if (value.IsValid)
                {
                    result = value;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, validator, parser: null, allowOptionalMappings: false, separator: null);

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
            => GetDictionary(key, validator, parser: null, allowOptionalMappings, separator);

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
            => GetDictionary(key, validator, parser, allowOptionalMappings: false, separator: null);

        private ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>>? parser, bool allowOptionalMappings, char? separator)
        {
            // Iterate in reverse order and keep the last successful value so the first source has precedence.
            var result = ConfigurationResult<IDictionary<string, string>>.NotFound();
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                ConfigurationResult<IDictionary<string, string>> value;
                if (parser is not null)
                {
                    value = source.GetDictionary(key, validator, parser);
                }
                else if (separator.HasValue)
                {
                    value = source.GetDictionary(key, validator, allowOptionalMappings, separator.Value);
                }
                else
                {
                    value = source.GetDictionary(key, validator);
                }

                if (value.IsValid)
                {
                    result = value;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<T> GetAs<T>(string key, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator)
        {
            // Iterate in reverse order and keep the last successful value so the first source has precedence.
            var result = ConfigurationResult<T>.NotFound();
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetAs(key, converter, validator);
                if (value.IsValid)
                {
                    result = value;
                }
            }

            return result;
        }
    }
}
