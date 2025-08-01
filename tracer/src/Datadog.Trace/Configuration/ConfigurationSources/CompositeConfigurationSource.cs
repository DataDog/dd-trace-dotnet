// <copyright file="CompositeConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents one or more configuration sources.
    /// </summary>
    internal class CompositeConfigurationSource : IConfigurationSource, IEnumerable<IConfigurationSource>
    {
        private readonly List<IConfigurationSource> _sources;

        public CompositeConfigurationSource()
        {
            _sources = new();
        }

        public CompositeConfigurationSource(IEnumerable<IConfigurationSource> sources)
        {
            _sources = [..sources];
        }

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
        [PublicApi]
        IEnumerator IEnumerable.GetEnumerator() => _sources.GetEnumerator();

        /// <inheritdoc />
        public bool IsPresent(string key)
            => _sources.Select(source => source.IsPresent(key)).FirstOrDefault(value => value);

        /// <inheritdoc />
        public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
            => _sources
              .Select(source => source.GetString(key, telemetry, validator, recordValue))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<string>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
            => _sources
              .Select(source => source.GetInt32(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<int>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
            => _sources
              .Select(source => source.GetDouble(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<double>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
            => _sources
              .Select(source => source.GetBool(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<bool>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => _sources
              .Select(source => source.GetDictionary(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
            => _sources
              .Select(source => source.GetDictionary(key, telemetry, validator, allowOptionalMappings, separator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
            => _sources
              .Select(source => source.GetDictionary(key, telemetry, validator, parser))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());

        /// <inheritdoc />
        public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
            => _sources
              .Select(source => source.GetAs<T>(key, telemetry, converter, validator, recordValue))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<T>.NotFound());
    }
}
