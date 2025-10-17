// <copyright file="ConfigurationBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration.ConfigurationSources.Registry;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

internal readonly struct ConfigurationBuilder(IConfigurationSource source, IConfigurationTelemetry telemetry)
{
    private readonly IConfigurationSource _source = source;
    private readonly IConfigurationTelemetry _telemetry = telemetry;

    public HasKeys<TKey> WithKeys<TKey>()
        where TKey : struct, IConfigKey
        => new(_source, _telemetry, new TKey());

    /// <summary>
    /// for test purposes
    /// </summary>
    internal HasKeys<TKey> WithKeys<TKey>(TKey obj)
        where TKey : struct, IConfigKey
        => new(_source, _telemetry, obj);

    public HasKeys<IntegrationNameConfigKey> WithIntegrationKey(string integrationName) => new(
        _source,
        _telemetry,
        new IntegrationNameConfigKey(integrationName),
        [
            string.Format(IntegrationSettings.IntegrationEnabledKey, integrationName),
            $"DD_{integrationName}_ENABLED"
        ]);

    public HasKeys<IntegrationAnalyticsEnabledConfigKey> WithIntegrationAnalyticsKey(string integrationName) => new(
        _source,
        _telemetry,
        new IntegrationAnalyticsEnabledConfigKey(integrationName),
#pragma warning disable 618 // App analytics is deprecated, but still used
        [
            string.Format(IntegrationSettings.AnalyticsEnabledKey, integrationName),
#pragma warning restore 618
            $"DD_{integrationName}_ANALYTICS_ENABLED"
        ]);

    public HasKeys<IntegrationAnalyticsSampleRateConfigKey> WithIntegrationAnalyticsSampleRateKey(string integrationName) => new(
        _source,
        _telemetry,
        new IntegrationAnalyticsSampleRateConfigKey(integrationName),
#pragma warning disable 618 // App analytics is deprecated, but still used
        [
            string.Format(IntegrationSettings.AnalyticsSampleRateKey, integrationName),
#pragma warning restore 618
            $"DD_{integrationName}_ANALYTICS_SAMPLE_RATE"
        ]);

    internal readonly struct HasKeys<TKey>(IConfigurationSource source, IConfigurationTelemetry telemetry, TKey key, string[]? providedAliases = null)
        where TKey : struct, IConfigKey
    {
        private readonly string[]? _providedAliases = providedAliases;
        private readonly string _keyString = key.GetKey(); // Cache the key string to avoid repeated GetKey() calls

        private IConfigurationSource Source { get; } = source;

        private IConfigurationTelemetry Telemetry { get; } = telemetry;

        private TKey Key { get; } = key;

        private string KeyString => _keyString;

        // ****************
        // String accessors
        // ****************
        public string? AsRedactedString()
            => AsString(defaultValue: null, validator: null, recordValue: false);

        public string AsRedactedString(string defaultValue)
            => AsString(defaultValue, validator: null, recordValue: false);

        /// <summary>
        /// Beware, this function won't record telemetry if the config isn't explicitly set.
        /// If you can, use <see cref="AsString(string)"/> instead or record telemetry manually.
        /// </summary>
        /// <returns>the string value of the configuration if set</returns>
        public string? AsString() => AsString(defaultValue: null, validator: null, recordValue: true);

        public string AsString(string defaultValue) => AsString(defaultValue, validator: null);

        /// <summary>
        /// Beware, this function won't record telemetry if the config isn't explicitly set.
        /// If you can, use <see cref="AsString(string, Func&lt;string, bool&gt;?)" /> instead or record telemetry manually.
        /// </summary>
        /// <returns>the string value of the configuration if set and valid</returns>
        public string? AsString(Func<string, bool> validator) => AsString(defaultValue: null, validator, recordValue: true);

        public string AsString(string defaultValue, Func<string, bool>? validator)
            => AsString(defaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator)
            => AsString(getDefaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>> converter)
            => AsString(getDefaultValue, validator, converter, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, bool recordValue)
            => AsString(getDefaultValue, validator, converter: null, recordValue);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        private string? AsString(string? defaultValue, Func<string, bool>? validator, bool recordValue)
        {
            // pre-record the default value, so it's in the "correct" place in the stack
            if (defaultValue is not null)
            {
                Telemetry.Record(KeyString, defaultValue, recordValue, ConfigurationOrigins.Default);
            }

            var result = GetStringResult(validator, converter: null, recordValue);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is not null && result.IsPresent)
            {
                // re-record telemetry because we found an invalid value in sources which clobbered it
                Telemetry.Record(KeyString, defaultValue, recordValue, ConfigurationOrigins.Default);
            }

            return defaultValue;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
        {
            // We don't "pre-record" the default because it's expensive to create
            var result = GetStringResult(validator, converter, recordValue);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (getDefaultValue is null)
            {
                return null;
            }

            var defaultValue = getDefaultValue();
            Telemetry.Record(KeyString, defaultValue, recordValue, ConfigurationOrigins.Default);
            return defaultValue;
        }

        // ****************
        // GetAs accessors
        // ****************
        // We have to use different methods for class/struct when we _don't_ have a null value, because NRTs don't work properly otherwise
        public T GetAs<T>(DefaultResult<T> defaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : notnull
        {
            // Ideally we would like to pre-record the default telemetry here so it's in the correct place
            // in the stack, but the GetAs<T> behaviour of the JsonConfigurationSource is problematic, as it
            // adds a telemetry result but still returns NotFound, so we can't use NotFound as the indicator
            // of whether we need to re-record the telemetry or not
            var result = GetAs(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            Telemetry.Record(KeyString, defaultValue.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
            return defaultValue.Result;
        }

        public T GetAs<T>(Func<DefaultResult<T>> getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : notnull
        {
            // We don't "pre-record" the default because it's expensive to create
            var result = GetAs(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            var defaultValue = getDefaultValue();
            Telemetry.Record(KeyString, defaultValue.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
            return defaultValue.Result;
        }

        public T? GetAsClass<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : class
        {
            var result = GetAs(validator, converter);
            return result is { Result: { } ddResult, IsValid: true } ? ddResult : null;
        }

        public T? GetAsStruct<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : struct
        {
            var result = GetAs(validator, converter);
            return result is { Result: { } ddResult, IsValid: true } ? ddResult : null;
        }

        // ****************
        // Bool accessors
        // ****************
        public bool? AsBool() => AsBool(defaultValue: null, validator: null, converter: null);

        public bool AsBool(bool defaultValue) => AsBool(defaultValue, validator: null);

        public bool? AsBool(Func<bool, bool> validator) => AsBool(defaultValue: null, validator, converter: null);

        public bool AsBool(bool defaultValue, Func<bool, bool>? validator)
            => AsBool(defaultValue, validator, converter: null).Value;

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator)
            => AsBool(getDefaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public bool? AsBool(bool? defaultValue, Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
            // pre-record the default value, so it's in the "correct" place in the stack
            if (defaultValue.HasValue)
            {
                Telemetry.Record(KeyString, defaultValue.Value, ConfigurationOrigins.Default);
            }

            var result = GetBoolResult(validator, converter: null);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is { } value && result.IsPresent)
            {
                Telemetry.Record(KeyString, value, ConfigurationOrigins.Default);
            }

            return defaultValue;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
            // We don't "pre-record" the default because it's expensive to create
            var result = GetBoolResult(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (getDefaultValue is null)
            {
                return null;
            }

            var defaultValue = getDefaultValue();
            Telemetry.Record(KeyString, defaultValue, ConfigurationOrigins.Default);
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
            => AsInt32(defaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))] // This doesn't work with nullables, but it still expresses intent
        public int? AsInt32(int? defaultValue, Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
        {
            // pre-record the default value, so it's in the "correct" place in the stack
            if (defaultValue.HasValue)
            {
                Telemetry.Record(KeyString, defaultValue.Value, ConfigurationOrigins.Default);
            }

            var result = GetInt32Result(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is { } value && result.IsPresent)
            {
                Telemetry.Record(KeyString, value, ConfigurationOrigins.Default);
            }

            return defaultValue;
        }

        // ****************
        // Double accessors
        // ****************
        public double? AsDouble() => AsDouble(defaultValue: null, validator: null);

        public double AsDouble(double defaultValue) => AsDouble(defaultValue, validator: null).Value;

        public double? AsDouble(Func<double, bool> validator) => AsDouble(null, validator);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public double? AsDouble(double? defaultValue, Func<double, bool>? validator)
            => AsDouble(defaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public double? AsDouble(double? defaultValue, Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
        {
            // pre-record the default value, so it's in the "correct" place in the stack
            if (defaultValue.HasValue)
            {
                Telemetry.Record(KeyString, defaultValue.Value, ConfigurationOrigins.Default);
            }

            var result = GetDoubleResult(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is { } value && result.IsPresent)
            {
                Telemetry.Record(KeyString, value, ConfigurationOrigins.Default);
            }

            return defaultValue;
        }

        // ****************
        // Dictionary accessors
        // ****************
        public IDictionary<string, string>? AsDictionary()
            => AsDictionary(allowOptionalMappings: false, getDefaultValue: null, defaultValueForTelemetry: string.Empty);

        public IDictionary<string, string>? AsDictionary(bool allowOptionalMappings)
            => AsDictionary(allowOptionalMappings, getDefaultValue: null, defaultValueForTelemetry: string.Empty);

        public IDictionary<string, string> AsDictionary(Func<IDictionary<string, string>> getDefaultValue, string defaultValueForTelemetry)
            => AsDictionary(allowOptionalMappings: false, getDefaultValue: getDefaultValue, defaultValueForTelemetry);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public IDictionary<string, string>? AsDictionary(IDictionary<string, string>? defaultValue, string defaultValueForTelemetry)
            => AsDictionary(allowOptionalMappings: false, defaultValue, defaultValueForTelemetry);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public IDictionary<string, string>? AsDictionary(
            bool allowOptionalMappings,
            Func<IDictionary<string, string>>? getDefaultValue,
            string defaultValueForTelemetry)
        {
            var result = GetDictionaryResult(allowOptionalMappings, separator: ':');
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (getDefaultValue?.Invoke() is not { } value)
            {
                return null;
            }

            Telemetry.Record(KeyString, defaultValueForTelemetry, recordValue: true, ConfigurationOrigins.Default);
            return value;
        }

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public IDictionary<string, string>? AsDictionary(
            bool allowOptionalMappings,
            IDictionary<string, string>? defaultValue,
            string defaultValueForTelemetry)
        {
            // pre-record the default value, so it's in the "correct" place in the stack
            if (defaultValue is not null)
            {
                Telemetry.Record(KeyString, defaultValueForTelemetry, recordValue: true, ConfigurationOrigins.Default);
            }

            var result = GetDictionaryResult(allowOptionalMappings, separator: ':');
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (result.IsPresent)
            {
                Telemetry.Record(KeyString, defaultValueForTelemetry, recordValue: true, ConfigurationOrigins.Default);
            }

            return defaultValue;
        }

        // ****************
        // Raw result accessors
        // ****************
        public ClassConfigurationResultWithKey<string> AsStringResult()
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetStringResult(validator: null, converter: null, recordValue: true));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetStringResult(validator: null, converter, recordValue: true));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetStringResult(validator, converter, recordValue: true));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult()
            => new(Telemetry, KeyString, recordValue: false, configurationResult: GetStringResult(validator: null, converter: null, recordValue: false));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult(Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, KeyString, recordValue: false, configurationResult: GetStringResult(validator: null, converter, recordValue: false));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, KeyString, recordValue: false, configurationResult: GetStringResult(validator, converter, recordValue: false));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
            => new(Telemetry, KeyString, recordValue, GetStringResult(validator, converter, recordValue));

        // bool
        public StructConfigurationResultWithKey<bool> AsBoolResult()
            => StructConfigurationResultWithKey<bool>.Create(Telemetry, KeyString, configurationResult: GetBoolResult(validator: null, converter: null));

        public StructConfigurationResultWithKey<bool> AsBoolResult(Func<string, ParsingResult<bool>>? converter)
            => StructConfigurationResultWithKey<bool>.Create(Telemetry, KeyString, configurationResult: GetBoolResult(validator: null, converter));

        public StructConfigurationResultWithKey<bool> AsBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
            => StructConfigurationResultWithKey<bool>.Create(Telemetry, KeyString, configurationResult: GetBoolResult(validator, converter));

        public ClassConfigurationResultWithKey<T> GetAsClassResult<T>(Func<string, ParsingResult<T>> converter)
            where T : class
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetAs(validator: null, converter));

        public ClassConfigurationResultWithKey<T> GetAsClassResult<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : class
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetAs(validator, converter));

        // int
        public StructConfigurationResultWithKey<int> AsInt32Result()
            => StructConfigurationResultWithKey<int>.Create(Telemetry, KeyString, configurationResult: GetInt32Result(validator: null, converter: null));

        public StructConfigurationResultWithKey<int> AsInt32Result(Func<string, ParsingResult<int>>? converter)
            => StructConfigurationResultWithKey<int>.Create(Telemetry, KeyString, configurationResult: GetInt32Result(validator: null, converter));

        public StructConfigurationResultWithKey<int> AsInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
            => StructConfigurationResultWithKey<int>.Create(Telemetry, KeyString, configurationResult: GetInt32Result(validator, converter));

        // double
        public StructConfigurationResultWithKey<double> AsDoubleResult()
            => StructConfigurationResultWithKey<double>.Create(Telemetry, KeyString, configurationResult: GetDoubleResult(validator: null, converter: null));

        public StructConfigurationResultWithKey<double> AsDoubleResult(Func<string, ParsingResult<double>>? converter)
            => StructConfigurationResultWithKey<double>.Create(Telemetry, KeyString, configurationResult: GetDoubleResult(validator: null, converter));

        public StructConfigurationResultWithKey<double> AsDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
            => StructConfigurationResultWithKey<double>.Create(Telemetry, KeyString, configurationResult: GetDoubleResult(validator, converter));

        // dictionary
        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult()
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings: false, separator: ':'));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(bool allowOptionalMappings)
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings, separator: ':'));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(char separator)
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings: false, separator));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(bool allowOptionalMappings, char separator)
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings, separator));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(Func<string, IDictionary<string, string>> parser)
            => new(Telemetry, KeyString, recordValue: true, configurationResult: GetDictionaryResult(parser));

        private ConfigurationResult<string> GetStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
        {
            var source = Source;
            var telemetry = Telemetry;
            return converter is null
                       ? GetResultWithFallback(
                           key => source.GetString(key, telemetry, validator, recordValue),
                           alias => source.GetString(alias, telemetry, validator, recordValue))
                       : GetResultWithFallback(
                           key => source.GetAs(key, telemetry, converter, validator, recordValue),
                           alias => source.GetAs(alias, telemetry, converter, validator, recordValue));
        }

        private ConfigurationResult<bool> GetBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
            var source = Source;
            var telemetry = Telemetry;
            return converter is null
                       ? GetResultWithFallback(
                           key => source.GetBool(key, telemetry, validator),
                           alias => source.GetBool(alias, telemetry, validator))
                       : GetResultWithFallback(
                           key => source.GetAs(key, telemetry, converter, validator, recordValue: true),
                           alias => source.GetAs(alias, telemetry, converter, validator, recordValue: true));
        }

        private ConfigurationResult<int> GetInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
        {
            var source = Source;
            var telemetry = Telemetry;
            return converter is null
                       ? GetResultWithFallback(
                           key => source.GetInt32(key, telemetry, validator),
                           alias => source.GetInt32(alias, telemetry, validator))
                       : GetResultWithFallback(
                           key => source.GetAs(key, telemetry, converter, validator, recordValue: true),
                           alias => source.GetAs(alias, telemetry, converter, validator, recordValue: true));
        }

        private ConfigurationResult<double> GetDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
        {
            var source = Source;
            var telemetry = Telemetry;
            return converter is null
                       ? GetResultWithFallback(
                           key => source.GetDouble(key, telemetry, validator),
                           alias => source.GetDouble(alias, telemetry, validator))
                       : GetResultWithFallback(
                           key => source.GetAs(key, telemetry, converter, validator, recordValue: true),
                           alias => source.GetAs(alias, telemetry, converter, validator, recordValue: true));
        }

        private ConfigurationResult<T> GetAs<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
        {
            var source = Source;
            var telemetry = Telemetry;
            return GetResultWithFallback(
                key => source.GetAs(key, telemetry, converter, validator, recordValue: true),
                alias => source.GetAs(alias, telemetry, converter, validator, recordValue: true));
        }

        private ConfigurationResult<IDictionary<string, string>> GetDictionaryResult(bool allowOptionalMappings, char separator)
        {
            var source = Source;
            var telemetry = Telemetry;
            return GetResultWithFallback(
                key => source.GetDictionary(key, telemetry, validator: null, allowOptionalMappings, separator),
                alias => source.GetDictionary(alias, telemetry, validator: null, allowOptionalMappings, separator));
        }

        private ConfigurationResult<IDictionary<string, string>> GetDictionaryResult(Func<string, IDictionary<string, string>> parser)
        {
            var source = Source;
            var telemetry = Telemetry;
            return GetResultWithFallback(
                key => source.GetDictionary(key, telemetry, validator: null, parser),
                alias => source.GetDictionary(alias, telemetry, validator: null, parser));
        }

        /// <summary>
        /// Common method that handles key resolution and alias fallback logic
        /// </summary>
        /// <param name="selector">The method to call for the primary key</param>
        /// <param name="aliasSelector">The method to call for alias keys</param>
        /// <typeparam name="T">The type being retrieved</typeparam>
        /// <returns>The raw <see cref="ConfigurationResult{T}"/></returns>
        private ConfigurationResult<T> GetResultWithFallback<T>(
            Func<TKey, ConfigurationResult<T>> selector,
            Func<ConfigKeyAlias, ConfigurationResult<T>> aliasSelector)
        {
            var result = selector(Key);
            if (!result.ShouldFallBack)
            {
                return result;
            }

            // GetAliases() now returns cached static arrays, so this is fast
            string[] aliases = _providedAliases ?? ConfigKeyAliasesSwitcher.GetAliases(KeyString);

            foreach (var alias in aliases)
            {
                result = aliasSelector(new ConfigKeyAlias(alias));
                if (!result.ShouldFallBack)
                {
                    break;
                }
            }

            return result;
        }
    }

    internal readonly struct StructConfigurationResultWithKey<T>
        where T : struct
    {
        public readonly string Key;
        public readonly IConfigurationTelemetry Telemetry;
        public readonly ConfigurationResult<T> ConfigurationResult;

        // Private so that it can only be created with specific T types
        private StructConfigurationResultWithKey(IConfigurationTelemetry telemetry, string key, ConfigurationResult<T> configurationResult)
        {
            Key = key;
            Telemetry = telemetry;
            ConfigurationResult = configurationResult;
        }

        public static StructConfigurationResultWithKey<bool> Create(IConfigurationTelemetry telemetry, string key, ConfigurationResult<bool> configurationResult)
            => new(telemetry, key, configurationResult);

        public static StructConfigurationResultWithKey<int> Create(IConfigurationTelemetry telemetry, string key, ConfigurationResult<int> configurationResult)
            => new(telemetry, key, configurationResult);

        public static StructConfigurationResultWithKey<double> Create(IConfigurationTelemetry telemetry, string key, ConfigurationResult<double> configurationResult) => new(telemetry, key, configurationResult);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public T? WithDefault(T? defaultValue)
        {
            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            RecordTelemetry(defaultValue);
            return defaultValue;
        }

        public T WithDefault(T defaultValue)
        {
            if (ConfigurationResult is { Result: var ddResult, IsValid: true })
            {
                return ddResult;
            }

            RecordTelemetry(defaultValue);
            return defaultValue;
        }

        public T? OverrideWith(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler)
            => CalculateOverrides(in otelConfig, overrideHandler, defaultValue: null);

        public T OverrideWith(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, T defaultValue)
            => CalculateOverrides(in otelConfig, overrideHandler, defaultValue).Value;

        [return: NotNullIfNotNull(nameof(defaultValue))]
        private T? CalculateOverrides(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, T? defaultValue)
        {
            if (overrideHandler.TryHandleOverrides(Key, ConfigurationResult, otelConfig.Key, otelConfig.ConfigurationResult, out var overridden))
            {
                return overridden;
            }

            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is null)
            {
                return null;
            }

            RecordTelemetry(defaultValue);
            return defaultValue;
        }

        private void RecordTelemetry(T? defaultValue)
        {
            switch (defaultValue)
            {
                case null:
                    break;
                case int intVal:
                    Telemetry.Record(Key, intVal, ConfigurationOrigins.Default);
                    break;
                case double doubleVal:
                    Telemetry.Record(Key, doubleVal, ConfigurationOrigins.Default);
                    break;
                case bool boolVal:
                    Telemetry.Record(Key, boolVal, ConfigurationOrigins.Default);
                    break;
            }
        }
    }

    internal readonly struct ClassConfigurationResultWithKey<T>(IConfigurationTelemetry telemetry, string key, bool recordValue, ConfigurationResult<T> configurationResult)
        where T : class
    {
        public readonly string Key = key;
        public readonly IConfigurationTelemetry Telemetry = telemetry;
        public readonly bool RecordValue = recordValue;
        public readonly ConfigurationResult<T> ConfigurationResult = configurationResult;

        public T WithDefault(DefaultResult<T> defaultValue)
        {
            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            Telemetry.Record(Key, defaultValue.TelemetryValue, RecordValue, ConfigurationOrigins.Default);
            return defaultValue.Result;
        }

        public T? OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler)
            => CalculateOverrides(in otelConfig, overrideHandler, defaultValue: null);

        public T OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, DefaultResult<T> defaultValue)
            => CalculateOverrides(in otelConfig, overrideHandler, defaultValue);

        public T OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, Func<DefaultResult<T>> getDefaultValue)
        {
            if (overrideHandler.TryHandleOverrides(Key, ConfigurationResult, otelConfig.Key, otelConfig.ConfigurationResult, out var overridden))
            {
                return overridden;
            }

            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            var defaultValue = getDefaultValue();
            Telemetry.Record(Key, defaultValue.TelemetryValue, RecordValue, ConfigurationOrigins.Default);
            return defaultValue.Result;
        }

        [return: NotNullIfNotNull(nameof(defaultValue))]
        private T? CalculateOverrides(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, DefaultResult<T>? defaultValue)
        {
            if (overrideHandler.TryHandleOverrides(Key, ConfigurationResult, otelConfig.Key, otelConfig.ConfigurationResult, out var overridden))
            {
                return overridden;
            }

            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is null)
            {
                return null;
            }

            Telemetry.Record(Key, defaultValue.Value.TelemetryValue, RecordValue, ConfigurationOrigins.Default);
            return defaultValue.Value.Result;
        }
    }

    private readonly struct ConfigKeyAlias(string alias) : IConfigKey
    {
        private readonly string _alias = alias;

        public string GetKey() => _alias;
    }
}
