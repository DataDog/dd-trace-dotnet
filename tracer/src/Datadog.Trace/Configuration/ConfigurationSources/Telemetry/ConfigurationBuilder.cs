// <copyright file="ConfigurationBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

internal readonly struct ConfigurationBuilder
{
    private readonly ITelemeteredConfigurationSource _source;
    private readonly IConfigurationTelemetry _telemetry;

    public ConfigurationBuilder(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        // If the source _isn't_ an ITelemeteredConfigurationSource, it's because it's a custom
        // IConfigurationSource implementation, so we treat that as a "Code" origin.
        _source = source as ITelemeteredConfigurationSource ?? new CustomTelemeteredConfigurationSource(source);
        _telemetry = telemetry;
    }

    public HasKeys WithKeys(string key) => new(_source, _telemetry, key);

    public HasKeys WithKeys(string key, string fallbackKey) => new(_source, _telemetry, key, fallbackKey);

    public HasKeys WithKeys(string key, string fallbackKey1, string fallbackKey2) => new(_source, _telemetry, key, fallbackKey1, fallbackKey2);

    public HasKeys WithKeys(string key, string fallbackKey1, string fallbackKey2, string fallbackKey3) => new(_source, _telemetry, key, fallbackKey1, fallbackKey2, fallbackKey3);

    internal readonly struct HasKeys
    {
        public HasKeys(ITelemeteredConfigurationSource source, IConfigurationTelemetry telemetry, string key, string? fallbackKey1 = null, string? fallbackKey2 = null, string? fallbackKey3 = null)
        {
            Source = source;
            Telemetry = telemetry;
            Key = key;
            FallbackKey1 = fallbackKey1;
            FallbackKey2 = fallbackKey2;
            FallbackKey3 = fallbackKey3;
        }

        private ITelemeteredConfigurationSource Source { get; }

        private IConfigurationTelemetry Telemetry { get; }

        private string Key { get; }

        private string? FallbackKey1 { get; }

        private string? FallbackKey2 { get; }

        private string? FallbackKey3 { get; }

        public StringAccessor AsString() => new(this, recordValue: true);

        public StringAccessor AsRedactedString() => new(this, recordValue: false);

        public BoolAccessor AsBool() => new(this);

        public Int32Accessor AsInt32() => new(this);

        public DictionaryAccessor AsDictionary() => new(this);

        public DoubleAccessor AsDouble() => new(this);

        internal readonly struct StringAccessor
        {
            private readonly HasKeys _keys;
            private readonly bool _recordValue;

            public StringAccessor(HasKeys keys, bool recordValue)
            {
                _keys = keys;
                _recordValue = recordValue;
            }

            public string? Get() => Get(getDefaultValue: null, validator: null);

            public string Get(string defaultValue) => Get(defaultValue, validator: null);

            public string? Get(Func<string, bool> validator) => Get(getDefaultValue: null, validator);

            public string Get(string defaultValue, Func<string, bool>? validator)
                => Get(() => defaultValue, validator);

            [return: NotNullIfNotNull(nameof(getDefaultValue))]
            public string? Get(Func<string>? getDefaultValue, Func<string, bool>? validator)
            {
                var result = _keys.Source.GetString(_keys.Key, _keys.Telemetry, validator, _recordValue)
                          ?? (_keys.FallbackKey1 is null ? null : _keys.Source.GetString(_keys.FallbackKey1, _keys.Telemetry, validator, _recordValue))
                          ?? (_keys.FallbackKey2 is null ? null : _keys.Source.GetString(_keys.FallbackKey2, _keys.Telemetry, validator, _recordValue))
                          ?? (_keys.FallbackKey3 is null ? null : _keys.Source.GetString(_keys.FallbackKey3, _keys.Telemetry, validator, _recordValue));

                // We have a valid value
                if (result is { Result: { } value, IsValid: true })
                {
                    return value;
                }

                // don't have a valid value
                if (getDefaultValue is null)
                {
                    return null;
                }

                var defaultValue = getDefaultValue();
                _keys.Telemetry.Record(_keys.Key, defaultValue, _recordValue, ConfigurationOrigins.Default);
                return defaultValue;
            }

            [return: NotNullIfNotNull(nameof(getDefaultValue))]
            public T? GetAs<T>(Func<T>? getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            {
                var result = _keys.Source.GetAs<T>(_keys.Key, _keys.Telemetry, converter, validator, _recordValue)
                          ?? (_keys.FallbackKey1 is null ? null : _keys.Source.GetAs<T>(_keys.FallbackKey1, _keys.Telemetry, converter, validator, _recordValue))
                          ?? (_keys.FallbackKey2 is null ? null : _keys.Source.GetAs<T>(_keys.FallbackKey2, _keys.Telemetry, converter, validator, _recordValue))
                          ?? (_keys.FallbackKey3 is null ? null : _keys.Source.GetAs<T>(_keys.FallbackKey3, _keys.Telemetry, converter, validator, _recordValue));

                // We have a valid value
                if (result is { Result: { } value, IsValid: true })
                {
                    return value;
                }

                // don't have a valid value
                if (getDefaultValue is null)
                {
                    return default;
                }

                var defaultValue = getDefaultValue();
                _keys.Telemetry.Record(_keys.Key, defaultValue?.ToString(), _recordValue, ConfigurationOrigins.Default);
                return defaultValue!;
            }
        }

        internal readonly struct BoolAccessor
        {
            private readonly HasKeys _keys;

            public BoolAccessor(HasKeys keys)
            {
                _keys = keys;
            }

            public bool? Get() => Get(getDefaultValue: null, validator: null);

            public bool Get(bool defaultValue) => Get(() => defaultValue, validator: null).Value;

            public bool? Get(Func<bool, bool> validator) => Get(null, validator);

            public bool Get(bool defaultValue, Func<bool, bool>? validator)
                => Get(() => defaultValue, validator).Value;

            [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
            public bool? Get(Func<bool>? getDefaultValue, Func<bool, bool>? validator)
            {
                var result = _keys.Source.GetBool(_keys.Key, _keys.Telemetry, validator)
                          ?? (_keys.FallbackKey1 is null ? null : _keys.Source.GetBool(_keys.FallbackKey1, _keys.Telemetry, validator))
                          ?? (_keys.FallbackKey2 is null ? null : _keys.Source.GetBool(_keys.FallbackKey2, _keys.Telemetry, validator))
                          ?? (_keys.FallbackKey3 is null ? null : _keys.Source.GetBool(_keys.FallbackKey3, _keys.Telemetry, validator));

                // We have a valid value
                if (result is { Result: { } value, IsValid: true })
                {
                    return value;
                }

                // don't have a default value
                if (getDefaultValue is null)
                {
                    return null;
                }

                var defaultValue = getDefaultValue();
                _keys.Telemetry.Record(_keys.Key, defaultValue, ConfigurationOrigins.Default);
                return defaultValue;
            }
        }

        internal readonly struct Int32Accessor
        {
            private readonly HasKeys _keys;

            public Int32Accessor(HasKeys keys)
            {
                _keys = keys;
            }

            public int? Get() => Get(defaultValue: null, validator: null);

            public int Get(int defaultValue) => Get(defaultValue, validator: null).Value;

            public int? Get(Func<int, bool> validator) => Get(null, validator);

            [return: NotNullIfNotNull(nameof(defaultValue))] // This doesn't work with nullables, but it still expresses intent
            public int? Get(int? defaultValue, Func<int, bool>? validator)
            {
                var result = _keys.Source.GetInt32(_keys.Key, _keys.Telemetry, validator)
                          ?? (_keys.FallbackKey1 is null ? null : _keys.Source.GetInt32(_keys.FallbackKey1, _keys.Telemetry, validator))
                          ?? (_keys.FallbackKey2 is null ? null : _keys.Source.GetInt32(_keys.FallbackKey2, _keys.Telemetry, validator))
                          ?? (_keys.FallbackKey3 is null ? null : _keys.Source.GetInt32(_keys.FallbackKey3, _keys.Telemetry, validator));

                // We have a valid value
                if (result is { Result: { } value, IsValid: true })
                {
                    return value;
                }

                // don't have a default value
                if (defaultValue is null)
                {
                    return null;
                }

                _keys.Telemetry.Record(_keys.Key, defaultValue.Value, ConfigurationOrigins.Default);
                return defaultValue.Value;
            }
        }

        internal readonly struct DictionaryAccessor
        {
            private readonly HasKeys _keys;

            public DictionaryAccessor(HasKeys keys)
            {
                _keys = keys;
            }

            public IDictionary<string, string>? Get() => Get(allowOptionalMappings: false);

            public IDictionary<string, string>? Get(bool allowOptionalMappings)
            {
                // TODO: Handle/allow default values + validation?
                var result = _keys.Source.GetDictionary(_keys.Key, _keys.Telemetry, validator: null, allowOptionalMappings)
                          ?? (_keys.FallbackKey1 is null ? null : _keys.Source.GetDictionary(_keys.FallbackKey1, _keys.Telemetry, validator: null, allowOptionalMappings))
                          ?? (_keys.FallbackKey2 is null ? null : _keys.Source.GetDictionary(_keys.FallbackKey2, _keys.Telemetry, validator: null, allowOptionalMappings))
                          ?? (_keys.FallbackKey3 is null ? null : _keys.Source.GetDictionary(_keys.FallbackKey3, _keys.Telemetry, validator: null, allowOptionalMappings));

                // We have a valid value
                if (result is { Result: { } value, IsValid: true })
                {
                    return value;
                }

                // Horrible that we have to stringify the dictionary, but that's all that's available in the telemetry api
                // _keys.Telemetry.Record(_keys.Key, string.Join(", ", defaultValue.Select(kvp => $"{kvp.Key}:{kvp.Value}")), ConfigurationOrigins.Default);
                // return defaultValue;
                return null;
            }
        }

        internal readonly struct DoubleAccessor
        {
            private readonly HasKeys _keys;

            public DoubleAccessor(HasKeys keys)
            {
                _keys = keys;
            }

            public double? Get() => Get(defaultValue: null, validator: null);

            public double Get(double defaultValue) => Get(defaultValue, validator: null).Value;

            public double? Get(Func<double, bool> validator) => Get(null, validator);

            [return: NotNullIfNotNull(nameof(defaultValue))]
            public double? Get(double? defaultValue, Func<double, bool>? validator)
            {
                var result = _keys.Source.GetDouble(_keys.Key, _keys.Telemetry, validator)
                          ?? (_keys.FallbackKey1 is null ? null : _keys.Source.GetDouble(_keys.FallbackKey1, _keys.Telemetry, validator))
                          ?? (_keys.FallbackKey2 is null ? null : _keys.Source.GetDouble(_keys.FallbackKey2, _keys.Telemetry, validator))
                          ?? (_keys.FallbackKey3 is null ? null : _keys.Source.GetDouble(_keys.FallbackKey3, _keys.Telemetry, validator));

                // We have a valid value
                if (result is { Result: { } value, IsValid: true })
                {
                    return value;
                }

                // don't have a default value
                if (defaultValue is null)
                {
                    return null;
                }

                _keys.Telemetry.Record(_keys.Key, defaultValue.Value, ConfigurationOrigins.Default);
                return defaultValue.Value;
            }
        }
    }
}
