// <copyright file="LayeredSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    internal class LayeredSource
    {
        internal class ConfigurationTelemetry : IConfigurationTelemetry
        {
            public void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null)
            {
                throw new System.NotImplementedException();
            }

            public void Record(string key, bool value, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null)
            {
                throw new System.NotImplementedException();
            }

            public void Record(string key, double value, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null)
            {
                throw new System.NotImplementedException();
            }

            public void Record(string key, int value, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null)
            {
                throw new System.NotImplementedException();
            }
        }

        internal LayeredSource(ConfigurationOrigins origin, IConfigurationSource? source)
        {
            Add(origin, source);
        }

        private readonly List<KeyValuePair<ConfigurationOrigins, IConfigurationSource>> _sources = new();
        private readonly IConfigurationTelemetry _telemetry = new ConfigurationTelemetry();

        public void Add(ConfigurationOrigins origin, IConfigurationSource? source)
        {
            if (source == null) { ThrowHelper.ThrowArgumentNullException(nameof(source)); }

            _sources.Add(new KeyValuePair<ConfigurationOrigins, IConfigurationSource>(origin, source));
        }

        // We could imagine having an override that would take a predicate in argument to validate the value as well
        public string GetString(string key, string defaultValue)
        {
            foreach (var source in _sources)
            {
                var value = source.Value.GetString(key);
                if (value != null)
                {
                    _telemetry.Record(key, value, true, source.Key, ConfigurationTelemetryErrorCode.None);
                    return value;
                }
            }

            _telemetry.Record(key, defaultValue, true, origin:ConfigurationOrigins.Default, ConfigurationTelemetryErrorCode.None);
            return defaultValue;
        }

        public int GetInt32(string key, int defaultValue)
        {
            foreach (var source in _sources)
            {
                var value = source.Value.GetInt32(key);
                if (value != null)
                {
                    _telemetry.Record(key, value.Value, source.Key, ConfigurationTelemetryErrorCode.None);
                    return value.Value;
                }
            }

            _telemetry.Record(key, defaultValue, origin:ConfigurationOrigins.Default, ConfigurationTelemetryErrorCode.None);
            return defaultValue;
        }

        public double GetDouble(string key, double defaultValue)
        {
            foreach (var source in _sources)
            {
                var value = source.Value.GetDouble(key);
                if (value != null)
                {
                    _telemetry.Record(key, value.Value, source.Key, ConfigurationTelemetryErrorCode.None);
                    return value.Value;
                }
            }

            _telemetry.Record(key, defaultValue, origin:ConfigurationOrigins.Default, ConfigurationTelemetryErrorCode.None);
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue)
        {
            foreach (var source in _sources)
            {
                var value = source.Value.GetBool(key);
                if (value != null)
                {
                    _telemetry.Record(key, value.Value, source.Key, ConfigurationTelemetryErrorCode.None);
                    return value.Value;
                }
            }

            _telemetry.Record(key, defaultValue, origin:ConfigurationOrigins.Default, ConfigurationTelemetryErrorCode.None);
            return defaultValue;
        }

        public (string, IDictionary<string, string>)? GetDictionary(string key)
        {
            foreach (var source in _sources)
            {
                var value = source.Value.GetDictionary(key);
                if (value != null)
                {
                    return (source.Key, value);
                }
            }

            return null;
        }

        public (string, IDictionary<string, string>)? GetDictionary(string key, bool allowOptionalMappings)
        {
            foreach (var source in _sources)
            {
                var value = source.Value.GetDictionary(key, allowOptionalMappings);
                if (value != null)
                {
                    return (source.Key, value);
                }
            }

            return null;
        }
    }
}
