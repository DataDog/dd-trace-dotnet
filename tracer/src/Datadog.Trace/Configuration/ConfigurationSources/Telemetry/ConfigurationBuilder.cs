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

internal readonly struct ConfigurationBuilder(IConfigurationSource source, IConfigurationTelemetry telemetry)
{
    private readonly IConfigurationSource _source = source;
    private readonly IConfigurationTelemetry _telemetry = telemetry;

    public HasKeys WithKeys(string key) => new(_source, _telemetry, key);

    public HasKeys WithKeys(string key, string fallbackKey) => new(_source, _telemetry, key, fallbackKey);

    public HasKeys WithKeys(string key, string fallbackKey1, string fallbackKey2) => new(_source, _telemetry, key, fallbackKey1, fallbackKey2);

    public HasKeys WithKeys(string key, string fallbackKey1, string fallbackKey2, string fallbackKey3) => new(_source, _telemetry, key, fallbackKey1, fallbackKey2, fallbackKey3);

    private static void RecordTelemetry<T>(IConfigurationTelemetry telemetry, string key, bool recordValue, T defaultValue)
    {
        switch (defaultValue)
        {
            case DefaultResult<T> defaultResult:
                telemetry.Record(key, defaultResult.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
                break;
            case int intVal:
                telemetry.Record(key, intVal, ConfigurationOrigins.Default);
                break;
            case double doubleVal:
                telemetry.Record(key, doubleVal, ConfigurationOrigins.Default);
                break;
            case bool boolVal:
                telemetry.Record(key, boolVal, ConfigurationOrigins.Default);
                break;
            case string stringVal:
                telemetry.Record(key, stringVal, recordValue, ConfigurationOrigins.Default);
                break;
            case null: // can't actually be called in practice
                break;
            default:
                // TODO: this shouldn't be calleable in practice, we need to revise it
                telemetry.Record(key, defaultValue.ToString(), recordValue, ConfigurationOrigins.Default);
                break;
        }
    }

    internal readonly struct HasKeys
    {
        public HasKeys(IConfigurationSource source, IConfigurationTelemetry telemetry, string key, string? fallbackKey1 = null, string? fallbackKey2 = null, string? fallbackKey3 = null)
        {
            Source = source;
            Telemetry = telemetry;
            Key = key;
            FallbackKey1 = fallbackKey1;
            FallbackKey2 = fallbackKey2;
            FallbackKey3 = fallbackKey3;
        }

        private IConfigurationSource Source { get; }

        private IConfigurationTelemetry Telemetry { get; }

        private string Key { get; }

        private string? FallbackKey1 { get; }

        private string? FallbackKey2 { get; }

        private string? FallbackKey3 { get; }

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
                Telemetry.Record(Key, defaultValue, recordValue, ConfigurationOrigins.Default);
            }

            var result = GetStringResult(validator, converter: null, recordValue);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is not null && result.IsPresent)
            {
                // re-record telemetry because we found an invalid value in sources which clobbered it
                Telemetry.Record(Key, defaultValue, recordValue, ConfigurationOrigins.Default);
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
            RecordTelemetry(Telemetry, Key, recordValue, defaultValue);
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

            Telemetry.Record(Key, defaultValue.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
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
            RecordTelemetry(Telemetry, Key, true, defaultValue.TelemetryValue);
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
                Telemetry.Record(Key, defaultValue.Value, ConfigurationOrigins.Default);
            }

            var result = GetBoolResult(validator, converter: null);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is { } value && result.IsPresent)
            {
                Telemetry.Record(Key, value, ConfigurationOrigins.Default);
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
            RecordTelemetry(Telemetry, Key, true, defaultValue);
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
                Telemetry.Record(Key, defaultValue.Value, ConfigurationOrigins.Default);
            }

            var result = GetInt32Result(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is { } value && result.IsPresent)
            {
                Telemetry.Record(Key, value, ConfigurationOrigins.Default);
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
                Telemetry.Record(Key, defaultValue.Value, ConfigurationOrigins.Default);
            }

            var result = GetDoubleResult(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (defaultValue is { } value && result.IsPresent)
            {
                Telemetry.Record(Key, value, ConfigurationOrigins.Default);
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

            Telemetry.Record(Key, defaultValueForTelemetry, recordValue: true, ConfigurationOrigins.Default);
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
                Telemetry.Record(Key, defaultValueForTelemetry, recordValue: true, ConfigurationOrigins.Default);
            }

            var result = GetDictionaryResult(allowOptionalMappings, separator: ':');
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (result.IsPresent)
            {
                Telemetry.Record(Key, defaultValueForTelemetry, recordValue: true, ConfigurationOrigins.Default);
            }

            return defaultValue;
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

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(Func<string, IDictionary<string, string>> parser)
            => new(Telemetry, Key, recordValue: true, configurationResult: GetDictionaryResult(parser));

        private ConfigurationResult<string> GetStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
            => converter is null
                   ? GetResult(Selectors.AsString, validator, recordValue)
                   : GetResult(Selectors.AsStringWithConverter, validator, converter, recordValue);

        private ConfigurationResult<bool> GetBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
            => converter is null
                   ? GetResult(Selectors.AsBool, validator, recordValue: true)
                   : GetResult(Selectors.AsBoolWithConverter, validator, converter, recordValue: true);

        private ConfigurationResult<int> GetInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
            => converter is null
                   ? GetResult(Selectors.AsInt32, validator, recordValue: true)
                   : GetResult(Selectors.AsInt32WithConverter, validator, converter, recordValue: true);

        private ConfigurationResult<double> GetDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
            => converter is null
                   ? GetResult(Selectors.AsDouble, validator, recordValue: true)
                   : GetResult(Selectors.AsDoubleWithConverter, validator, converter, recordValue: true);

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
        private ConfigurationResult<T> GetResult<T>(Func<IConfigurationSource, string, IConfigurationTelemetry, Func<T, bool>?, bool, ConfigurationResult<T>> selector, Func<T, bool>? validator, bool recordValue)
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
        /// <param name="converter">The converter to run when calling <see cref="IConfigurationSource.GetAs{T}"/></param>
        /// <param name="recordValue">If applicable, whether to record the value in configuration</param>
        /// <typeparam name="T">The type being retrieved</typeparam>
        /// <returns>The raw <see cref="ConfigurationResult{T}"/></returns>
        private ConfigurationResult<T> GetResult<T>(Func<IConfigurationSource, string, IConfigurationTelemetry, Func<T, bool>?, Func<string, ParsingResult<T>>, bool, ConfigurationResult<T>> selector, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter, bool recordValue)
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

        private ConfigurationResult<IDictionary<string, string>> GetDictionaryResult(Func<string, IDictionary<string, string>> parser)
        {
            var result = Source.GetDictionary(Key, Telemetry, validator: null, parser);
            if (result.ShouldFallBack && FallbackKey1 is not null)
            {
                result = Source.GetDictionary(FallbackKey1, Telemetry, validator: null, parser);
            }

            if (result.ShouldFallBack && FallbackKey2 is not null)
            {
                result = Source.GetDictionary(FallbackKey2, Telemetry, validator: null, parser);
            }

            if (result.ShouldFallBack && FallbackKey3 is not null)
            {
                result = Source.GetDictionary(FallbackKey3, Telemetry, validator: null, parser);
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
        {
            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            RecordTelemetry(Telemetry, Key, RecordValue, defaultValue);
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

            RecordTelemetry(Telemetry, Key, RecordValue, defaultValue);
            return defaultValue;
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
        {
            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            RecordTelemetry(Telemetry, Key, RecordValue, defaultValue);
            return defaultValue;
        }

        public T? OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler)
            => CalculateOverrides(in otelConfig, overrideHandler, defaultValue: null);

        public T OverrideWith(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, T defaultValue)
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
            RecordTelemetry(Telemetry, Key, RecordValue, defaultValue.TelemetryValue);
            return defaultValue.Result;
        }

        [return: NotNullIfNotNull(nameof(defaultValue))]
        private T? CalculateOverrides(in ClassConfigurationResultWithKey<T> otelConfig, IConfigurationOverrideHandler overrideHandler, T? defaultValue)
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

            RecordTelemetry(Telemetry, Key, RecordValue, defaultValue);
            return defaultValue;
        }
    }

    private static class Selectors
    {
        // static accessor functions
        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<string, bool>?, bool, ConfigurationResult<string>> AsString
            = (source, key, telemetry, validator, recordValue) => source.GetString(key, telemetry, validator, recordValue);

        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<bool, bool>?, bool, ConfigurationResult<bool>> AsBool
            = (source, key, telemetry, validator, _) => source.GetBool(key, telemetry, validator);

        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<int, bool>?, bool, ConfigurationResult<int>> AsInt32
            = (source, key, telemetry, validator, _) => source.GetInt32(key, telemetry, validator);

        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<double, bool>?, bool, ConfigurationResult<double>> AsDouble
            = (source, key, telemetry, validator, _) => source.GetDouble(key, telemetry, validator);

        // static accessor functions with converters
        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<string, bool>?, Func<string, ParsingResult<string>>, bool, ConfigurationResult<string>> AsStringWithConverter
            = (source, key, telemetry, validator, converter, recordValue) => source.GetAs(key, telemetry, converter, validator, recordValue);

        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<bool, bool>?, Func<string, ParsingResult<bool>>, bool, ConfigurationResult<bool>> AsBoolWithConverter
            = (source, key, telemetry, validator, converter, _) => source.GetAs(key, telemetry, converter, validator, recordValue: true);

        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<int, bool>?, Func<string, ParsingResult<int>>, bool, ConfigurationResult<int>> AsInt32WithConverter
            = (source, key, telemetry, validator, converter, _) => source.GetAs(key, telemetry, converter, validator, recordValue: true);

        internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, Func<double, bool>?, Func<string, ParsingResult<double>>, bool, ConfigurationResult<double>> AsDoubleWithConverter
            = (source, key, telemetry, validator, converter, _) => source.GetAs(key, telemetry, converter, validator, recordValue: true);
    }
}
