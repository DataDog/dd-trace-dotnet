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
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents one or more configuration sources.
    /// </summary>
    public class CompositeConfigurationSource : IConfigurationSource, IEnumerable<IConfigurationSource>, ITelemeteredConfigurationSource
    {
        private readonly List<ITelemeteredConfigurationSource> _sources = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeConfigurationSource"/> class.
        /// </summary>
        [PublicApi]
        public CompositeConfigurationSource()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.CompositeConfigurationSource_Ctor);
        }

        private protected CompositeConfigurationSource(bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is to give us a non-public API we can use
        }

        /// <summary>
        /// Adds a new configuration source to this instance.
        /// </summary>
        /// <param name="source">The configuration source to add.</param>
        [PublicApi]
        public void Add(IConfigurationSource source)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.CompositeConfigurationSource_Add);
            if (source == null) { ThrowHelper.ThrowArgumentNullException(nameof(source)); }

            AddInternal(source);
        }

        /// <summary>
        /// Inserts an element into the <see cref="CompositeConfigurationSource"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The configuration source to insert.</param>
        [PublicApi]
        public void Insert(int index, IConfigurationSource item)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.CompositeConfigurationSource_Insert);
            if (item == null) { ThrowHelper.ThrowArgumentNullException(nameof(item)); }

            InsertInternal(index, item);
        }

        /// <summary>
        /// Gets the <see cref="string"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        public string? GetString(string key)
        {
            var value = _sources
                  .Select(source => source.GetString(key, NullConfigurationTelemetry.Instance, validator: null, recordValue: true))
                  .FirstOrDefault(value => value.IsValid, ConfigurationResult<string>.NotFound());
            return value.IsValid ? value.Result : null;
        }

        /// <summary>
        /// Gets the <see cref="int"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        public int? GetInt32(string key)
        {
            var value = _sources
                       .Select(source => source.GetInt32(key, NullConfigurationTelemetry.Instance, validator: null))
                       .FirstOrDefault(value => value.IsValid, ConfigurationResult<int>.NotFound());
            return value.IsValid ? value.Result : null;
        }

        /// <summary>
        /// Gets the <see cref="double"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        public double? GetDouble(string key)
        {
            var value = _sources
                       .Select(source => source.GetDouble(key, NullConfigurationTelemetry.Instance, validator: null))
                       .FirstOrDefault(value => value.IsValid, ConfigurationResult<double>.NotFound());
            return value.IsValid ? value.Result : null;
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value of the first setting found with
        /// the specified key from the current list of configuration sources.
        /// Sources are queried in the order in which they were added.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        public bool? GetBool(string key)
        {
            var value = _sources
                       .Select(source => source.GetBool(key, NullConfigurationTelemetry.Instance, validator: null))
                       .FirstOrDefault(value => value.IsValid, ConfigurationResult<bool>.NotFound());
            return value.IsValid ? value.Result : null;
        }

        internal void AddInternal(IConfigurationSource source)
        {
            var telemeteredSource = source as ITelemeteredConfigurationSource ?? new CustomTelemeteredConfigurationSource(source);
            _sources.Add(telemeteredSource);
        }

        internal void InsertInternal(int index, IConfigurationSource source)
        {
            var telemeteredSource = source as ITelemeteredConfigurationSource ?? new CustomTelemeteredConfigurationSource(source);
            _sources.Insert(index, telemeteredSource);
        }

        /// <inheritdoc />
        [PublicApi]
        IEnumerator<IConfigurationSource> IEnumerable<IConfigurationSource>.GetEnumerator()
        {
            return _sources
                  .Select(
                       x => x as IConfigurationSource
                         ?? ((CustomTelemeteredConfigurationSource)x).Source)
                  .GetEnumerator();
        }

        /// <inheritdoc />
        [PublicApi]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _sources
                  .Select(
                       x => x as IConfigurationSource
                         ?? ((CustomTelemeteredConfigurationSource)x).Source)
                  .GetEnumerator();
        }

        /// <inheritdoc />
        [PublicApi]
        public IDictionary<string, string>? GetDictionary(string key)
        {
            var value = _sources
                       .Select(source => source.GetDictionary(key, NullConfigurationTelemetry.Instance, validator: null))
                       .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());
            return value.IsValid ? value.Result : null;
        }

        /// <inheritdoc />
        [PublicApi]
        public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings)
        {
            var value = _sources
                       .Select(source => source.GetDictionary(key, NullConfigurationTelemetry.Instance, validator: null, allowOptionalMappings, separator: ':'))
                       .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());
            return value.IsValid ? value.Result : null;
        }

        /// <inheritdoc />
        bool ITelemeteredConfigurationSource.IsPresent(string key)
            => _sources.Select(source => source.IsPresent(key)).FirstOrDefault(value => value);

        /// <inheritdoc />
        ConfigurationResult<string> ITelemeteredConfigurationSource.GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
            => _sources
              .Select(source => source.GetString(key, telemetry, validator, recordValue))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<string>.NotFound());

        /// <inheritdoc />
        ConfigurationResult<int> ITelemeteredConfigurationSource.GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
            => _sources
              .Select(source => source.GetInt32(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<int>.NotFound());

        /// <inheritdoc />
        ConfigurationResult<double> ITelemeteredConfigurationSource.GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
            => _sources
              .Select(source => source.GetDouble(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<double>.NotFound());

        /// <inheritdoc />
        ConfigurationResult<bool> ITelemeteredConfigurationSource.GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
            => _sources
              .Select(source => source.GetBool(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<bool>.NotFound());

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>> ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => _sources
              .Select(source => source.GetDictionary(key, telemetry, validator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>> ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
            => _sources
              .Select(source => source.GetDictionary(key, telemetry, validator, allowOptionalMappings, separator))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<IDictionary<string, string>>.NotFound());

        /// <inheritdoc />
        ConfigurationResult<T> ITelemeteredConfigurationSource.GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
            => _sources
              .Select(source => source.GetAs<T>(key, telemetry, converter, validator, recordValue))
              .FirstOrDefault(value => value.IsValid, ConfigurationResult<T>.NotFound());
    }
}
