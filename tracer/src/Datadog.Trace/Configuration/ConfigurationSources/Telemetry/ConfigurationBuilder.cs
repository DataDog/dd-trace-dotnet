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

        // ****************
        // String accessors
        // ****************
        public string? AsRedactedString()
            => AsString(getDefaultValue: null, validator: null, recordValue: false);

        public string AsRedactedString(string defaultValue)
            => AsString(() => defaultValue, validator: null, recordValue: false);

        public string? AsString() => AsString(getDefaultValue: null, validator: null, recordValue: true);

        public string AsString(string defaultValue) => AsString(defaultValue, validator: null);

        public string? AsString(Func<string, bool> validator) => AsString(getDefaultValue: null, validator, recordValue: true);

        public string AsString(string defaultValue, Func<string, bool>? validator)
            => AsString(() => defaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator)
            => AsString(getDefaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, bool recordValue)
        {
            var result = Source.GetString(Key, Telemetry, validator, recordValue)
                      ?? (FallbackKey1 is null ? null : Source.GetString(FallbackKey1, Telemetry, validator, recordValue))
                      ?? (FallbackKey2 is null ? null : Source.GetString(FallbackKey2, Telemetry, validator, recordValue))
                      ?? (FallbackKey3 is null ? null : Source.GetString(FallbackKey3, Telemetry, validator, recordValue));

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
            Telemetry.Record(Key, defaultValue, recordValue, ConfigurationOrigins.Default);
            return defaultValue;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public T? GetAs<T>(Func<T>? getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
        {
            var result = Source.GetAs<T>(Key, Telemetry, converter, validator, recordValue: true)
                      ?? (FallbackKey1 is null ? null : Source.GetAs<T>(FallbackKey1, Telemetry, converter, validator, recordValue: true))
                      ?? (FallbackKey2 is null ? null : Source.GetAs<T>(FallbackKey2, Telemetry, converter, validator, recordValue: true))
                      ?? (FallbackKey3 is null ? null : Source.GetAs<T>(FallbackKey3, Telemetry, converter, validator, recordValue: true));

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
            Telemetry.Record(Key, defaultValue?.ToString(), recordValue: true, ConfigurationOrigins.Default);
            return defaultValue!;
        }

        // ****************
        // Bool accessors
        // ****************
        public bool? AsBool() => AsBool(getDefaultValue: null, validator: null);

        public bool AsBool(bool defaultValue) => AsBool(() => defaultValue, validator: null).Value;

        public bool? AsBool(Func<bool, bool> validator) => AsBool(null, validator);

        public bool AsBool(bool defaultValue, Func<bool, bool>? validator)
            => AsBool(() => defaultValue, validator).Value;

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator)
        {
            var result = Source.GetBool(Key, Telemetry, validator)
                      ?? (FallbackKey1 is null ? null : Source.GetBool(FallbackKey1, Telemetry, validator))
                      ?? (FallbackKey2 is null ? null : Source.GetBool(FallbackKey2, Telemetry, validator))
                      ?? (FallbackKey3 is null ? null : Source.GetBool(FallbackKey3, Telemetry, validator));

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
            Telemetry.Record(Key, defaultValue, ConfigurationOrigins.Default);
            return defaultValue;
        }

        // ****************
        // Int32 accessors
        // ****************
        public int? AsInt32() => AsInt32(defaultValue: null, validator: null);

        public int AsInt32(int defaultValue) => AsInt32(defaultValue, validator: null).Value;

        public int? AsInt32(Func<int, bool> validator) => AsInt32(null, validator);

        [return: NotNullIfNotNull(nameof(defaultValue))] // This doesn't work with nullables, but it still expresses intent
        public int? AsInt32(int? defaultValue, Func<int, bool>? validator)
        {
            var result = Source.GetInt32(Key, Telemetry, validator)
                      ?? (FallbackKey1 is null ? null : Source.GetInt32(FallbackKey1, Telemetry, validator))
                      ?? (FallbackKey2 is null ? null : Source.GetInt32(FallbackKey2, Telemetry, validator))
                      ?? (FallbackKey3 is null ? null : Source.GetInt32(FallbackKey3, Telemetry, validator));

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

            Telemetry.Record(Key, defaultValue.Value, ConfigurationOrigins.Default);
            return defaultValue.Value;
        }

        public double? AsDouble() => AsDouble(defaultValue: null, validator: null);

        public double AsDouble(double defaultValue) => AsDouble(defaultValue, validator: null).Value;

        public double? AsDouble(Func<double, bool> validator) => AsDouble(null, validator);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public double? AsDouble(double? defaultValue, Func<double, bool>? validator)
        {
            var result = Source.GetDouble(Key, Telemetry, validator)
                      ?? (FallbackKey1 is null ? null : Source.GetDouble(FallbackKey1, Telemetry, validator))
                      ?? (FallbackKey2 is null ? null : Source.GetDouble(FallbackKey2, Telemetry, validator))
                      ?? (FallbackKey3 is null ? null : Source.GetDouble(FallbackKey3, Telemetry, validator));

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

            Telemetry.Record(Key, defaultValue.Value, ConfigurationOrigins.Default);
            return defaultValue.Value;
        }

        // ****************
        // Dictionary accessors
        // ****************
        public IDictionary<string, string>? AsDictionary() => AsDictionary(allowOptionalMappings: false);

        public IDictionary<string, string>? AsDictionary(bool allowOptionalMappings)
        {
            // TODO: Handle/allow default values + validation?
            var result = Source.GetDictionary(Key, Telemetry, validator: null, allowOptionalMappings)
                      ?? (FallbackKey1 is null ? null : Source.GetDictionary(FallbackKey1, Telemetry, validator: null, allowOptionalMappings))
                      ?? (FallbackKey2 is null ? null : Source.GetDictionary(FallbackKey2, Telemetry, validator: null, allowOptionalMappings))
                      ?? (FallbackKey3 is null ? null : Source.GetDictionary(FallbackKey3, Telemetry, validator: null, allowOptionalMappings));

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
}
