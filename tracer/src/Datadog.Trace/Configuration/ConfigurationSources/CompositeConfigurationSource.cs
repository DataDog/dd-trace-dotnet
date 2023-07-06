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

            var telemeteredSource = item as ITelemeteredConfigurationSource ?? new CustomTelemeteredConfigurationSource(item);
            _sources.Insert(index, telemeteredSource);
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
            foreach (var source in _sources)
            {
                if (source.GetString(key, NullConfigurationTelemetry.Instance, validator: null, recordValue: true) is { } result)
                {
                    return result.Result;
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
        [PublicApi]
        public int? GetInt32(string key)
        {
            return _sources.Select(source => source.GetInt32(key, NullConfigurationTelemetry.Instance, validator: null))
                           .FirstOrDefault(value => value != null)?.Result;
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
            foreach (var source in _sources)
            {
                if (source.GetDouble(key, NullConfigurationTelemetry.Instance, validator: null) is { } result)
                {
                    return result.Result;
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
        [PublicApi]
        public bool? GetBool(string key)
        {
            foreach (var source in _sources)
            {
                if (source.GetBool(key, NullConfigurationTelemetry.Instance, validator: null) is { } result)
                {
                    return result.Result;
                }
            }

            return default;
        }

        internal void AddInternal(IConfigurationSource source)
        {
            var telemeteredSource = source as ITelemeteredConfigurationSource ?? new CustomTelemeteredConfigurationSource(source);
            _sources.Add(telemeteredSource);
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
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, NullConfigurationTelemetry.Instance, validator: null) is { } result)
                {
                    return result.Result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        [PublicApi]
        public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, NullConfigurationTelemetry.Instance, validator: null, allowOptionalMappings) is { } result)
                {
                    return result.Result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        [PublicApi]
        public unsafe IDictionary<string, string>? GetDictionary(string key, delegate*<ref string, ref string, bool> selector)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, NullConfigurationTelemetry.Instance, validator: null, selector: selector) is { } result)
                {
                    return result.Result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        [PublicApi]
        public unsafe IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, NullConfigurationTelemetry.Instance, validator: null, allowOptionalMappings, selector: selector) is { } result)
                {
                    return result.Result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<string>? ITelemeteredConfigurationSource.GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        {
            foreach (var source in _sources)
            {
                if (source.GetString(key, telemetry, validator, recordValue) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<int>? ITelemeteredConfigurationSource.GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        {
            foreach (var source in _sources)
            {
                if (source.GetInt32(key, telemetry, validator) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<double>? ITelemeteredConfigurationSource.GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        {
            foreach (var source in _sources)
            {
                if (source.GetDouble(key, telemetry, validator) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<bool>? ITelemeteredConfigurationSource.GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        {
            foreach (var source in _sources)
            {
                if (source.GetBool(key, telemetry, validator) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, telemetry, validator) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, telemetry, validator, allowOptionalMappings) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        unsafe ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, delegate*<ref string, ref string, bool> selector)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, telemetry, validator, selector) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        unsafe ConfigurationResult<IDictionary<string, string>>? ITelemeteredConfigurationSource.GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
        {
            foreach (var source in _sources)
            {
                if (source.GetDictionary(key, telemetry, validator, allowOptionalMappings, selector) is { } result)
                {
                    return result;
                }
            }

            return default;
        }

        /// <inheritdoc />
        ConfigurationResult<T>? ITelemeteredConfigurationSource.GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        {
            foreach (var source in _sources)
            {
                if (source.GetAs(key, telemetry, converter, validator, recordValue) is { } result)
                {
                    return result;
                }
            }

            return default;
        }
    }
}
