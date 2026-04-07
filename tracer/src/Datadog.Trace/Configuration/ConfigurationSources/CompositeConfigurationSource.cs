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

        public ConfigurationOrigins Origin => ConfigurationOrigins.Unknown;

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
        public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        {
            // We iterate in reverse order, and keep the last successful value
            // because we need to record the data for all the sources in telemetry
            // We also have to keep track of whether the last value was the last _found_ value
            // as we need to "restore" the telemetry if so.
            var result = ConfigurationResult<string>.NotFound();
            var isLastFound = false;
            var origin = ConfigurationOrigins.Unknown;
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetString(key, telemetry, validator, recordValue);
                if (value.IsValid)
                {
                    result = value;
                    isLastFound = true;
                    origin = source.Origin;
                }
                else if (value.IsPresent)
                {
                    isLastFound = false;
                }
            }

            if (result.IsValid && !isLastFound)
            {
                telemetry.Record(key, result.Result, recordValue, origin);
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        {
            // We iterate in reverse order, and keep the last successful value
            // because we need to record the data for all the sources in telemetry
            var result = ConfigurationResult<int>.NotFound();
            var isLastFound = false;
            var origin = ConfigurationOrigins.Unknown;
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetInt32(key, telemetry, validator);
                if (value.IsValid)
                {
                    result = value;
                    isLastFound = true;
                    origin = source.Origin;
                }
                else if (value.IsPresent)
                {
                    isLastFound = false;
                }
            }

            if (result.IsValid && !isLastFound)
            {
                telemetry.Record(key, result.Result, origin);
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        {
            // We iterate in reverse order, and keep the last successful value
            // because we need to record the data for all the sources in telemetry
            var result = ConfigurationResult<double>.NotFound();
            var isLastFound = false;
            var origin = ConfigurationOrigins.Unknown;
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetDouble(key, telemetry, validator);
                if (value.IsValid)
                {
                    result = value;
                    isLastFound = true;
                    origin = source.Origin;
                }
                else if (value.IsPresent)
                {
                    isLastFound = false;
                }
            }

            if (result.IsValid && !isLastFound)
            {
                telemetry.Record(key, result.Result, origin);
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        {
            // We iterate in reverse order, and keep the last successful value
            // because we need to record the data for all the sources in telemetry
            var result = ConfigurationResult<bool>.NotFound();
            var isLastFound = false;
            var origin = ConfigurationOrigins.Unknown;
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetBool(key, telemetry, validator);
                if (value.IsValid)
                {
                    result = value;
                    isLastFound = true;
                    origin = source.Origin;
                }
                else if (value.IsPresent)
                {
                    isLastFound = false;
                }
            }

            if (result.IsValid && !isLastFound)
            {
                telemetry.Record(key, result.Result, origin);
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
            => GetDictionary(key, telemetry, validator, parser: null, allowOptionalMappings: false, separator: null);

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
            => GetDictionary(key, telemetry, validator, parser: null, allowOptionalMappings, separator);

        /// <inheritdoc />
        public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
            => GetDictionary(key, telemetry, validator, parser, allowOptionalMappings: false, separator: null);

        private ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>>? parser, bool allowOptionalMappings, char? separator)
        {
            // We iterate in reverse order, and keep the last successful value
            // because we need to record the data for all the sources in telemetry
            var result = ConfigurationResult<IDictionary<string, string>>.NotFound();
            var isLastFound = false;
            var origin = ConfigurationOrigins.Unknown;
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                ConfigurationResult<IDictionary<string, string>> value;
                if (parser is not null)
                {
                    value = source.GetDictionary(key, telemetry, validator, parser);
                }
                else if (separator.HasValue)
                {
                    value = source.GetDictionary(key, telemetry, validator, allowOptionalMappings, separator.Value);
                }
                else
                {
                    value = source.GetDictionary(key, telemetry, validator);
                }

                if (value.IsValid)
                {
                    result = value;
                    isLastFound = true;
                    origin = source.Origin;
                }
                else if (value.IsPresent)
                {
                    isLastFound = false;
                }
            }

            if (result.IsValid && !isLastFound)
            {
                // there should always be a telemetry override by convention, so just record a sentinel for now if there's not for some reason
                telemetry.Record(key, result.TelemetryOverride ?? "<MISSING>", recordValue: true, origin);
            }

            return result;
        }

        /// <inheritdoc />
        public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        {
            // We iterate in reverse order, and keep the last successful value
            // because we need to record the data for all the sources in telemetry
            var result = ConfigurationResult<T>.NotFound();
            var isLastFound = false;
            var origin = ConfigurationOrigins.Unknown;
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var value = source.GetAs(key, telemetry, converter, validator, recordValue);
                if (value.IsValid)
                {
                    result = value;
                    isLastFound = true;
                    origin = source.Origin;
                }
                else if (value.IsPresent)
                {
                    isLastFound = false;
                }
            }

            if (result.IsValid && !isLastFound)
            {
                telemetry.Record(key, result.TelemetryOverride ?? result.Result?.ToString(), recordValue: true, origin);
            }

            return result;
        }
    }
}
