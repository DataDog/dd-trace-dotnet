// <copyright file="ConfigurationBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

internal readonly struct ConfigurationBuilder
{
    // static accessor functions
    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<string, bool>?, bool, ConfigurationResult<string>> AsStringSelector
        = (source, key, telemetry, validator, recordValue) => source.GetString(key, telemetry, validator, recordValue);

    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<bool, bool>?, bool, ConfigurationResult<bool>> AsBoolSelector
        = (source, key, telemetry, validator, _) => source.GetBool(key, telemetry, validator);

    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<int, bool>?, bool, ConfigurationResult<int>> AsInt32Selector
        = (source, key, telemetry, validator, _) => source.GetInt32(key, telemetry, validator);

    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<double, bool>?, bool, ConfigurationResult<double>> AsDoubleSelector
        = (source, key, telemetry, validator, _) => source.GetDouble(key, telemetry, validator);

    // static accessor functions with converters
    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<string, bool>?, Func<string, ParsingResult<string>>, bool, ConfigurationResult<string>> AsStringWithConverterSelector
        = (source, key, telemetry, validator, converter, recordValue) => source.GetAs(key, telemetry, converter, validator, recordValue);

    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<bool, bool>?, Func<string, ParsingResult<bool>>, bool, ConfigurationResult<bool>> AsBoolWithConverterSelector
        = (source, key, telemetry, validator, converter, _) => source.GetAs(key, telemetry, converter, validator, recordValue: true);

    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<int, bool>?, Func<string, ParsingResult<int>>, bool, ConfigurationResult<int>> AsInt32WithConverterSelector
        = (source, key, telemetry, validator, converter, _) => source.GetAs(key, telemetry, converter, validator, recordValue: true);

    private static readonly Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<double, bool>?, Func<string, ParsingResult<double>>, bool, ConfigurationResult<double>> AsDoubleWithConverterSelector
        = (source, key, telemetry, validator, converter, _) => source.GetAs(key, telemetry, converter, validator, recordValue: true);

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

    private static bool TryHandleResult<T>(
        IConfigurationTelemetry telemetry,
        string key,
        ConfigurationResult<T> result,
        bool recordValue,
        Func<DefaultResult<T>>? getDefaultValue,
        [NotNullIfNotNull(nameof(getDefaultValue))] out T? value)
    {
        if (result is { Result: { } ddResult, IsValid: true })
        {
            value = ddResult;
            return true;
        }

        // don't have a default value, so caller (which knows what the <T> is needs
        // to return the default value. Necessary because we can't create a generic
        // method that works "correctly" for both value types and reference types.
        if (getDefaultValue is null)
        {
            value = default;
            return false;
        }

        var defaultValue = getDefaultValue();
        RecordTelemetry(telemetry, key, recordValue, defaultValue);

        // The compiler complains about this, because technically you _could_ call it as `TryHandleResult<int?>` (for example)
        // in which case Func<DefaultResult<T>> _could_ return a `null` value, so the `[NotNullIfNotNull]` annotation
        // would be wrong. In practice, we control the calls to this method, and we know that T is always non-null
        // so it's safe to use the dammit here.
        value = defaultValue.Result!;
        return true;
    }

    private static void RecordTelemetry<T>(IConfigurationTelemetry telemetry, string key, bool recordValue, DefaultResult<T> defaultValue)
    {
        switch (defaultValue.Result)
        {
            case int intVal:
                telemetry.Record(key, intVal, ConfigurationOrigins.Default);
                break;
            case double doubleVal:
                telemetry.Record(key, doubleVal, ConfigurationOrigins.Default);
                break;
            case bool boolVal:
                telemetry.Record(key, boolVal, ConfigurationOrigins.Default);
                break;
            default:
                telemetry.Record(key, defaultValue.TelemetryValue, recordValue, ConfigurationOrigins.Default);
                break;
        }
    }

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
            => AsString(getDefaultValue: null, validator: null, converter: null, recordValue: false);

        public string AsRedactedString(string defaultValue)
            => AsString(() => defaultValue, validator: null, converter: null, recordValue: false);

        /// <summary>
        /// Beware, this function won't record telemetry if the config isn't explicitly set.
        /// If you can, use <see cref="AsString(string)"/> instead or record telemetry manually.
        /// </summary>
        /// <returns>the string value of the configuration if set</returns>
        public string? AsString() => AsString(getDefaultValue: null, validator: null, converter: null, recordValue: true);

        public string AsString(string defaultValue) => AsString(defaultValue, validator: null);

        /// <summary>
        /// Beware, this function won't record telemetry if the config isn't explicitly set.
        /// If you can, use <see cref="AsString(string, Func&lt;string, bool&gt;?)" /> instead or record telemetry manually.
        /// </summary>
        /// <returns>the string value of the configuration if set and valid</returns>
        public string? AsString(Func<string, bool> validator) => AsString(getDefaultValue: null, validator, recordValue: true);

        public string AsString(string defaultValue, Func<string, bool>? validator)
            => AsString(() => defaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<DefaultResult<string>>? getDefaultValue, Func<string, bool>? validator)
            => AsString(getDefaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<DefaultResult<string>>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>> converter)
            => AsString(getDefaultValue, validator, converter, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<DefaultResult<string>>? getDefaultValue, Func<string, bool>? validator, bool recordValue)
            => AsString(getDefaultValue, validator, converter: null, recordValue);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<DefaultResult<string>>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
        {
            var result = GetStringResult(validator, converter, recordValue);
            return TryHandleResult(Telemetry, Key, result, recordValue, getDefaultValue, out var value) ? value : null;
        }

        // We have to use different methods for class/struct when we _don't_ have a null value, because NRTs don't work properly otherwise
        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public T GetAs<T>(Func<DefaultResult<T>> getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
        {
            var result = GetAs(validator, converter);
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue, out var value)
                       ? value
                       : default!; // TryHandleResult always returns true as getDefaultValue != null
        }

        public T? GetAsClass<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : class
        {
            var result = GetAs(validator, converter);
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue: null, out var value)
                       ? value
                       : null;
        }

        public T? GetAsStruct<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : struct
        {
            var result = GetAs(validator, converter);
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue: null, out var value)
                       ? value
                       : null;
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
        public bool? AsBool(Func<DefaultResult<bool>>? getDefaultValue, Func<bool, bool>? validator)
            => AsBool(getDefaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<DefaultResult<bool>>? getDefaultValue, Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
            var result = GetBoolResult(validator, converter);
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue, out var value) ? value : null;
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
            var result = GetInt32Result(validator, converter);
            Func<DefaultResult<int>>? getDefaultValue = defaultValue.HasValue ? () => defaultValue.Value : null;
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue, out var value) ? value : null;
        }

        public double? AsDouble() => AsDouble(defaultValue: null, validator: null);

        public double AsDouble(double defaultValue) => AsDouble(defaultValue, validator: null).Value;

        public double? AsDouble(Func<double, bool> validator) => AsDouble(null, validator);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public double? AsDouble(double? defaultValue, Func<double, bool>? validator)
            => AsDouble(defaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public double? AsDouble(double? defaultValue, Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
        {
            var result = GetDoubleResult(validator, converter);
            Func<DefaultResult<double>>? getDefaultValue = defaultValue.HasValue ? () => defaultValue.Value : null;
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue, out var value) ? value : null;
        }

        // ****************
        // Dictionary accessors
        // ****************
        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public IDictionary<string, string>? AsDictionary(Func<DefaultResult<IDictionary<string, string>>>? getDefaultValue = null) => AsDictionary(allowOptionalMappings: false, getDefaultValue: getDefaultValue);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public IDictionary<string, string>? AsDictionary(bool allowOptionalMappings, Func<DefaultResult<IDictionary<string, string>>>? getDefaultValue = null)
        {
            // TODO: Handle/allow default values + validation?
            var result = GetDictionaryResult(allowOptionalMappings, separator: ':');
            return TryHandleResult(Telemetry, Key, result, recordValue: true, getDefaultValue, out var value) ? value : null;
        }

        // ****************
        // Raw result accessors
        // ****************
        public ClassConfigurationResultWithKey<string> AsStringResult()
            => new(Telemetry, Key, recordValue: true, configurationResult: GetStringResult(validator: null, converter: null, recordValue: true));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetStringResult(validator: null, converter, recordValue: true));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetStringResult(validator, converter, recordValue: true));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult()
            => new(Telemetry, Key, recordValue: false, configurationResult: GetStringResult(validator: null, converter: null, recordValue: false));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult(Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, Key, recordValue: false, configurationResult: GetStringResult(validator: null, converter, recordValue: false));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
            => new(Telemetry, Key, recordValue: false, configurationResult: GetStringResult(validator, converter, recordValue: false));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
            => new(Telemetry, Key, recordValue, GetStringResult(validator, converter, recordValue));

        // bool
        public StructConfigurationResultWithKey<bool> AsBoolResult()
            => new(Telemetry, Key, recordValue: true, configurationResult: GetBoolResult(validator: null, converter: null));

        public StructConfigurationResultWithKey<bool> AsBoolResult(Func<string, ParsingResult<bool>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetBoolResult(validator: null, converter));

        public StructConfigurationResultWithKey<bool> AsBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetBoolResult(validator, converter));

        // T
        public ClassConfigurationResultWithKey<T> GetAsClassResult<T>(Func<string, ParsingResult<T>> converter)
            where T : class
            => new(Telemetry, Key, recordValue: true, configurationResult: GetAs(validator: null, converter));

        public ClassConfigurationResultWithKey<T> GetAsClassResult<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : class
            => new(Telemetry, Key, recordValue: true, configurationResult: GetAs(validator, converter));

        public StructConfigurationResultWithKey<T> GetAsStructResult<T>(Func<string, ParsingResult<T>> converter)
            where T : struct
            => new(Telemetry, Key, recordValue: true, configurationResult: GetAs(validator: null, converter));

        public StructConfigurationResultWithKey<T> GetAsStructResult<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : struct
            => new(Telemetry, Key, recordValue: true, configurationResult: GetAs(validator, converter));

        // int
        public StructConfigurationResultWithKey<int> AsInt32Result()
            => new(Telemetry, Key, recordValue: true, configurationResult: GetInt32Result(validator: null, converter: null));

        public StructConfigurationResultWithKey<int> AsInt32Result(Func<string, ParsingResult<int>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetInt32Result(validator: null, converter));

        public StructConfigurationResultWithKey<int> AsInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetInt32Result(validator, converter));

        // double
        public StructConfigurationResultWithKey<double> AsDoubleResult()
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDoubleResult(validator: null, converter: null));

        public StructConfigurationResultWithKey<double> AsDoubleResult(Func<string, ParsingResult<double>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDoubleResult(validator: null, converter));

        public StructConfigurationResultWithKey<double> AsDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDoubleResult(validator, converter));

        // dictionary
        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult()
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings: false, separator: ':'));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(bool allowOptionalMappings)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings, separator: ':'));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(char separator)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings: false, separator));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(bool allowOptionalMappings, char separator)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDictionaryResult(allowOptionalMappings, separator));

        private ConfigurationResult<string> GetStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
            => converter is null
                   ? GetResult(AsStringSelector, validator, recordValue)
                   : GetResult(AsStringWithConverterSelector, validator, converter, recordValue);

        private ConfigurationResult<bool> GetBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
            => converter is null
                   ? GetResult(AsBoolSelector, validator, recordValue: true)
                   : GetResult(AsBoolWithConverterSelector, validator, converter, recordValue: true);

        private ConfigurationResult<int> GetInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
            => converter is null
                   ? GetResult(AsInt32Selector, validator, recordValue: true)
                   : GetResult(AsInt32WithConverterSelector, validator, converter, recordValue: true);

        private ConfigurationResult<double> GetDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
            => converter is null
                   ? GetResult(AsDoubleSelector, validator, recordValue: true)
                   : GetResult(AsDoubleWithConverterSelector, validator, converter, recordValue: true);

        private ConfigurationResult<T> GetAs<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            => GetResult(
                (source, key, telemetry, val, convert, recordValue) => source.GetAs(key, telemetry, convert!, val, recordValue),
                validator,
                converter,
                recordValue: true);

        /// <summary>
        /// Gets the raw <see cref="ConfigurationResult{T}"/> from the configuration source, recording the access in telemetry
        /// </summary>
        /// <param name="selector">The method to invoke to retrieve the parameter</param>
        /// <param name="validator">The validator to call to decide if a provided value is valid</param>
        /// <param name="recordValue">If applicable, whether to record the value in configuration</param>
        /// <typeparam name="T">The type being retrieved</typeparam>
        /// <returns>The raw <see cref="ConfigurationResult{T}"/></returns>
        private ConfigurationResult<T> GetResult<T>(Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<T, bool>?, bool, ConfigurationResult<T>> selector, Func<T, bool>? validator, bool recordValue)
        {
            var result = selector(Source, Key, Telemetry, validator, recordValue);
            if (result.ShouldFallBack && FallbackKey1 is not null)
            {
                result = selector(Source, FallbackKey1, Telemetry, validator, recordValue);
            }

            if (result.ShouldFallBack && FallbackKey2 is not null)
            {
                result = selector(Source, FallbackKey2, Telemetry, validator, recordValue);
            }

            if (result.ShouldFallBack && FallbackKey3 is not null)
            {
                result = selector(Source, FallbackKey3, Telemetry, validator, recordValue);
            }

            return result;
        }

        /// <summary>
        /// Gets the raw <see cref="ConfigurationResult{T}"/> from the configuration source, recording the access in telemetry
        /// </summary>
        /// <param name="selector">The method to invoke to retrieve the parameter</param>
        /// <param name="validator">The validator to call to decide if a provided value is valid</param>
        /// <param name="converter">The converter to run when calling <see cref="ITelemeteredConfigurationSource.GetAs{T}"/></param>
        /// <param name="recordValue">If applicable, whether to record the value in configuration</param>
        /// <typeparam name="T">The type being retrieved</typeparam>
        /// <returns>The raw <see cref="ConfigurationResult{T}"/></returns>
        private ConfigurationResult<T> GetResult<T>(Func<ITelemeteredConfigurationSource, string, IConfigurationTelemetry, Func<T, bool>?, Func<string, ParsingResult<T>>, bool, ConfigurationResult<T>> selector, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter, bool recordValue)
        {
            var result = selector(Source, Key, Telemetry, validator, converter, recordValue);
            if (result.ShouldFallBack && FallbackKey1 is not null)
            {
                result = selector(Source, FallbackKey1, Telemetry, validator, converter, recordValue);
            }

            if (result.ShouldFallBack && FallbackKey2 is not null)
            {
                result = selector(Source, FallbackKey2, Telemetry, validator, converter, recordValue);
            }

            if (result.ShouldFallBack && FallbackKey3 is not null)
            {
                result = selector(Source, FallbackKey3, Telemetry, validator, converter, recordValue);
            }

            return result;
        }

        private ConfigurationResult<IDictionary<string, string>> GetDictionaryResult(bool allowOptionalMappings, char separator)
        {
            var result = Source.GetDictionary(Key, Telemetry, validator: null, allowOptionalMappings, separator);
            if (result.ShouldFallBack && FallbackKey1 is not null)
            {
                result = Source.GetDictionary(FallbackKey1, Telemetry, validator: null, allowOptionalMappings, separator);
            }

            if (result.ShouldFallBack && FallbackKey2 is not null)
            {
                result = Source.GetDictionary(FallbackKey2, Telemetry, validator: null, allowOptionalMappings, separator);
            }

            if (result.ShouldFallBack && FallbackKey3 is not null)
            {
                result = Source.GetDictionary(FallbackKey3, Telemetry, validator: null, allowOptionalMappings, separator);
            }

            return result;
        }
    }

    internal readonly struct StructConfigurationResultWithKey<T>(IConfigurationTelemetry telemetry, string key, bool recordValue, ConfigurationResult<T> configurationResult)
        where T : struct
    {
        public readonly string Key = key;
        public readonly IConfigurationTelemetry Telemetry = telemetry;
        public readonly bool RecordValue = recordValue;
        public readonly ConfigurationResult<T> ConfigurationResult = configurationResult;

        public T WithDefault(T defaultValue)
            => WithDefault(getDefaultValue: () => defaultValue);

        public T WithDefault(Func<DefaultResult<T>> getDefaultValue)
        {
            if (TryHandleResult(Telemetry, Key, ConfigurationResult, RecordValue, getDefaultValue, out var value))
            {
                return value;
            }

            return default; // should never be invoked because we have a value for getDefaultValue
        }

        public T? OverrideWith(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler)
            => CalculateOverrides(in otelConfig, overrideHandler, getDefaultValue: null);

        public T OverrideWith(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, T defaultValue)
            => CalculateOverrides(in otelConfig, overrideHandler, getDefaultValue: () => defaultValue).Value;

        public T OverrideWith(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, Func<DefaultResult<T>> getDefaultValue)
            => CalculateOverrides(in otelConfig, overrideHandler, getDefaultValue).Value;

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private T? CalculateOverrides(in StructConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, Func<DefaultResult<T>>? getDefaultValue)
        {
            if (overrideHandler.TryHandleOverrides(Key, ConfigurationResult, otelConfig.Key, otelConfig.ConfigurationResult, out var overridden))
            {
                return overridden;
            }

            if (TryHandleResult(Telemetry, Key, ConfigurationResult, RecordValue, getDefaultValue, out var value))
            {
                return value;
            }

            // need to return default/default value here depending on whether it's a struct
            return null;
        }
    }

    internal readonly struct ClassConfigurationResultWithKey<T>(IConfigurationTelemetry telemetry, string key, bool recordValue, ConfigurationResult<T> configurationResult)
        where T : class
    {
        public readonly string Key = key;
        public readonly IConfigurationTelemetry Telemetry = telemetry;
        public readonly bool RecordValue = recordValue;
        public readonly ConfigurationResult<T> ConfigurationResult = configurationResult;

        public T WithDefault(T defaultValue)
            => WithDefault(getDefaultValue: () => defaultValue);

        public T WithDefault(Func<DefaultResult<T>> getDefaultValue)
        {
            if (TryHandleResult(Telemetry, Key, ConfigurationResult, RecordValue, getDefaultValue, out var value))
            {
                return value;
            }

            return default!; // should never be invoked because we have a value for getDefaultValue
        }

        public T? OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler)
            => CalculateOverrides(in otelConfig, overrideHandler, getDefaultValue: null);

        public T OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, T defaultValue)
            => CalculateOverrides(in otelConfig, overrideHandler, getDefaultValue: () => defaultValue);

        public T OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, Func<DefaultResult<T>> getDefaultValue)
            => CalculateOverrides(in otelConfig, overrideHandler, getDefaultValue);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private T? CalculateOverrides(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, Func<DefaultResult<T>>? getDefaultValue)
        {
            if (overrideHandler.TryHandleOverrides(Key, ConfigurationResult, otelConfig.Key, otelConfig.ConfigurationResult, out var overridden))
            {
                return overridden;
            }

            if (TryHandleResult(Telemetry, Key, ConfigurationResult, RecordValue, getDefaultValue, out var value))
            {
                return value;
            }

            // need to return default/default value here depending on whether it's a struct
            return null;
        }
    }
}
