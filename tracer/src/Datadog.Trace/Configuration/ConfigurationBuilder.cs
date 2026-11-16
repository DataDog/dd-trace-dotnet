// <copyright file="ConfigurationBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Datadog.Trace.Configuration;

internal readonly struct ConfigurationBuilder(IConfigurationSource source)
{
    private readonly IConfigurationSource _source = source;

    public HasKeys WithKeys(string key) => new(_source, key);

    public HasKeys WithIntegrationKey(string integrationName)
    {
        var integrationEnabledKeys = IntegrationNameToKeys.GetIntegrationEnabledKeys(integrationName);
        return new(
            _source,
            integrationEnabledKeys.Key,
            integrationEnabledKeys.Value);
    }

    public HasKeys WithIntegrationAnalyticsKey(string integrationName)
    {
#pragma warning disable 618 // App analytics is deprecated, but still used
        var integrationAnalyticsEnabledKeys = IntegrationNameToKeys.GetIntegrationAnalyticsEnabledKeys(integrationName);
#pragma warning restore 618
        return new(
            _source,
            integrationAnalyticsEnabledKeys.Key,
            integrationAnalyticsEnabledKeys.Value);
    }

    public HasKeys WithIntegrationAnalyticsSampleRateKey(string integrationName)
    {
#pragma warning disable 618 // App analytics is deprecated, but still used
        var integrationAnalyticsSampleRateKeys = IntegrationNameToKeys.GetIntegrationAnalyticsSampleRateKeys(integrationName);
#pragma warning restore 618
        return new(
            _source,
            integrationAnalyticsSampleRateKeys.Key,
            integrationAnalyticsSampleRateKeys.Value);
    }

    internal readonly struct HasKeys(IConfigurationSource source, string key, string[]? providedAliases = null)
    {
        private readonly string[]? _providedAliases = providedAliases;

        private IConfigurationSource Source { get; } = source;

        private string Key { get; } = key;

        // ****************
        // String accessors
        // ****************
        public string? AsRedactedString()
            => ResolveString(defaultValue: null, validator: null);

        public string AsRedactedString(string defaultValue)
            => ResolveString(defaultValue, validator: null);

        /// <returns>the string value of the configuration if set</returns>
        public string? AsString() => ResolveString(defaultValue: null, validator: null);

        public string AsString(string defaultValue) => ResolveString(defaultValue, validator: null);

        /// <returns>the string value of the configuration if set and valid</returns>
        public string? AsString(Func<string, bool> validator) => ResolveString(defaultValue: null, validator);

        public string AsString(string defaultValue, Func<string, bool>? validator)
            => ResolveString(defaultValue, validator);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator)
            => ResolveString(getDefaultValue, validator);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>> converter)
            => ResolveString(getDefaultValue, validator, converter);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? ResolveString(Func<string>? getDefaultValue, Func<string, bool>? validator)
            => ResolveString(getDefaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        private string? ResolveString(string? defaultValue, Func<string, bool>? validator)
        {
            var result = GetStringResult(validator, converter: null);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            return defaultValue;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? ResolveString(Func<string>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
        {
            var result = GetStringResult(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            if (getDefaultValue is null)
            {
                return null;
            }

            var defaultValue = getDefaultValue();
            return defaultValue;
        }

        // ****************
        // GetAs accessors
        // ****************
        // We have to use different methods for class/struct when we _don't_ have a null value, because NRTs don't work properly otherwise
        public T GetAs<T>(DefaultResult<T> defaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : notnull
        {
            var result = GetAs(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            return defaultValue.Result;
        }

        public T GetAs<T>(Func<DefaultResult<T>> getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : notnull
        {
            var result = GetAs(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            var defaultValue = getDefaultValue();
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
            var result = GetBoolResult(validator, converter: null);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            return defaultValue;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
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
            var result = GetInt32Result(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
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
            var result = GetDoubleResult(validator, converter);
            if (result is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            return defaultValue;
        }

        // ****************
        // Dictionary accessors
        // ****************
        public IDictionary<string, string>? AsDictionary()
            => AsDictionary(allowOptionalMappings: false);

        public IDictionary<string, string>? AsDictionary(bool allowOptionalMappings)
        {
            var result = GetDictionaryResult(allowOptionalMappings, separator: ':');
            return result is { Result: { } value, IsValid: true } ? value : null;
        }

        // ****************
        // Raw result accessors
        // ****************
        public ClassConfigurationResultWithKey<string> AsStringResult()
            => new(Key, GetStringResult(validator: null, converter: null));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, ParsingResult<string>>? converter)
            => new(Key, GetStringResult(validator: null, converter));

        public ClassConfigurationResultWithKey<string> AsStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
            => new(Key, GetStringResult(validator, converter));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult()
            => new(Key, GetStringResult(validator: null, converter: null));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult(Func<string, ParsingResult<string>>? converter)
            => new(Key, GetStringResult(validator: null, converter));

        public ClassConfigurationResultWithKey<string> AsRedactedStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
            => new(Key, GetStringResult(validator, converter));

        // bool
        public StructConfigurationResultWithKey<bool> AsBoolResult()
            => StructConfigurationResultWithKey<bool>.Create(Key, configurationResult: GetBoolResult(validator: null, converter: null));

        public StructConfigurationResultWithKey<bool> AsBoolResult(Func<string, ParsingResult<bool>>? converter)
            => StructConfigurationResultWithKey<bool>.Create(Key, configurationResult: GetBoolResult(validator: null, converter));

        public StructConfigurationResultWithKey<bool> AsBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
            => StructConfigurationResultWithKey<bool>.Create(Key, configurationResult: GetBoolResult(validator, converter));

        // T
        public ClassConfigurationResultWithKey<T> GetAsClassResult<T>(Func<string, ParsingResult<T>> converter)
            where T : class
            => new(Key, GetAs(validator: null, converter));

        public ClassConfigurationResultWithKey<T> GetAsClassResult<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
            where T : class
            => new(Key, GetAs(validator, converter));

        // int
        public StructConfigurationResultWithKey<int> AsInt32Result()
            => StructConfigurationResultWithKey<int>.Create(Key, configurationResult: GetInt32Result(validator: null, converter: null));

        public StructConfigurationResultWithKey<int> AsInt32Result(Func<string, ParsingResult<int>>? converter)
            => StructConfigurationResultWithKey<int>.Create(Key, configurationResult: GetInt32Result(validator: null, converter));

        public StructConfigurationResultWithKey<int> AsInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
            => StructConfigurationResultWithKey<int>.Create(Key, configurationResult: GetInt32Result(validator, converter));

        // double
        public StructConfigurationResultWithKey<double> AsDoubleResult()
            => StructConfigurationResultWithKey<double>.Create(Key, configurationResult: GetDoubleResult(validator: null, converter: null));

        public StructConfigurationResultWithKey<double> AsDoubleResult(Func<string, ParsingResult<double>>? converter)
            => StructConfigurationResultWithKey<double>.Create(Key, configurationResult: GetDoubleResult(validator: null, converter));

        public StructConfigurationResultWithKey<double> AsDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
            => StructConfigurationResultWithKey<double>.Create(Key, configurationResult: GetDoubleResult(validator, converter));

        // dictionary
        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult()
            => new(Key, GetDictionaryResult(allowOptionalMappings: false, separator: ':'));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(bool allowOptionalMappings)
            => new(Key, GetDictionaryResult(allowOptionalMappings, separator: ':'));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(char separator)
            => new(Key, GetDictionaryResult(allowOptionalMappings: false, separator));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(bool allowOptionalMappings, char separator)
            => new(Key, GetDictionaryResult(allowOptionalMappings, separator));

        public ClassConfigurationResultWithKey<IDictionary<string, string>> AsDictionaryResult(Func<string, IDictionary<string, string>> parser)
            => new(Key, GetDictionaryResult(parser));

        private ConfigurationResult<string> GetStringResult(Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter)
        {
            var source = Source;
            return converter is null
                ? GetResultWithFallback(key => source.GetString(key, validator))
                : GetResultWithFallback(key => source.GetAs(key, converter, validator));
        }

        private ConfigurationResult<bool> GetBoolResult(Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
            var source = Source;
            return converter is null
                ? GetResultWithFallback(key => source.GetBool(key, validator))
                : GetResultWithFallback(key => source.GetAs(key, converter, validator));
        }

        private ConfigurationResult<int> GetInt32Result(Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
        {
            var source = Source;
            return converter is null
                ? GetResultWithFallback(key => source.GetInt32(key, validator))
                : GetResultWithFallback(key => source.GetAs(key, converter, validator));
        }

        private ConfigurationResult<double> GetDoubleResult(Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
        {
            var source = Source;
            return converter is null
                ? GetResultWithFallback(key => source.GetDouble(key, validator))
                : GetResultWithFallback(key => source.GetAs(key, converter, validator));
        }

        private ConfigurationResult<T> GetAs<T>(Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
        {
            var source = Source;
            return GetResultWithFallback(key => source.GetAs(key, converter, validator));
        }

        private ConfigurationResult<IDictionary<string, string>> GetDictionaryResult(bool allowOptionalMappings, char separator)
        {
            var source = Source;
            return GetResultWithFallback(key => source.GetDictionary(key, validator: null, allowOptionalMappings, separator));
        }

        private ConfigurationResult<IDictionary<string, string>> GetDictionaryResult(Func<string, IDictionary<string, string>> parser)
        {
            var source = Source;
            return GetResultWithFallback(key => source.GetDictionary(key, validator: null, parser));
        }

        /// <summary>
        /// Common method that handles key resolution and alias fallback logic
        /// </summary>
        /// <param name="selector">The method to call for each key</param>
        /// <typeparam name="T">The type being retrieved</typeparam>
        /// <returns>The raw <see cref="ConfigurationResult{T}"/></returns>
        private ConfigurationResult<T> GetResultWithFallback<T>(Func<string, ConfigurationResult<T>> selector)
        {
            var result = selector(Key);
            if (!result.ShouldFallBack)
            {
                return result;
            }

            var aliases = _providedAliases ?? ConfigKeyAliasesSwitcher.GetAliases(Key);

            foreach (var alias in aliases)
            {
                result = selector(alias);
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
        public readonly ConfigurationResult<T> ConfigurationResult;

        // Private so that it can only be created with specific T types
        private StructConfigurationResultWithKey(string key, ConfigurationResult<T> configurationResult)
        {
            Key = key;
            ConfigurationResult = configurationResult;
        }

        public static StructConfigurationResultWithKey<bool> Create(string key, ConfigurationResult<bool> configurationResult)
            => new(key, configurationResult);

        public static StructConfigurationResultWithKey<int> Create(string key, ConfigurationResult<int> configurationResult)
            => new(key, configurationResult);

        public static StructConfigurationResultWithKey<double> Create(string key, ConfigurationResult<double> configurationResult) => new(key, configurationResult);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public T? WithDefault(T? defaultValue)
        {
            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

            return defaultValue;
        }

        public T WithDefault(T defaultValue)
        {
            if (ConfigurationResult is { Result: var ddResult, IsValid: true })
            {
                return ddResult;
            }

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

            return defaultValue;
        }
    }

    internal readonly struct ClassConfigurationResultWithKey<T>(string key, ConfigurationResult<T> configurationResult)
        where T : class
    {
        public readonly string Key = key;
        public readonly ConfigurationResult<T> ConfigurationResult = configurationResult;

        public T WithDefault(DefaultResult<T> defaultValue)
        {
            if (ConfigurationResult is { Result: { } ddResult, IsValid: true })
            {
                return ddResult;
            }

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

            return defaultValue.Value.Result;
        }
    }
}
