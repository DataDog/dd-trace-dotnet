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
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

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
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator)
            => AsString(getDefaultValue, validator, recordValue: true);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>> converter)
            => AsString(getDefaultValue, validator, converter, recordValue: true);

        public string? AsStringWithOpenTelemetryMapping(string openTelemetryKey, Func<string, ParsingResult<string>>? openTelemetryConverter = null)
            => AsString(getDefaultValue: null, validator: null, recordValue: true, openTelemetryKey, openTelemetryConverter);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, bool recordValue)
            => AsString(getDefaultValue, validator, converter: null, recordValue);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, Func<string, ParsingResult<string>>? converter, bool recordValue)
        {
            var result = GetStringResult(validator, converter, recordValue);

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
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, bool recordValue, string openTelemetryKey, Func<string, ParsingResult<string>>? openTelemetryConverter)
        {
            var datadogConfigResult = GetResult(AsStringSelector, validator, recordValue);

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            if (datadogConfigResult.IsPresent && Source.IsPresent(openTelemetryKey))
            {
                // TODO Log to user and report "otel.env.hiding" telemetry metric
            }
            else if (Source.IsPresent(openTelemetryKey))
            {
                var openTelemetryResult = openTelemetryConverter switch
                {
                    null => Source.GetString(openTelemetryKey, Telemetry, validator, recordValue),
                    _ => Source.GetAs(openTelemetryKey, Telemetry, openTelemetryConverter, validator, recordValue),
                };

                if (openTelemetryResult is { Result: { } openTelemetryValue, IsValid: true })
                {
                    return openTelemetryValue;
                }
                else
                {
                    // TODO Log to user and report "otel.env.invalid" telemetry metric
                }
            }

            if (datadogConfigResult.IsValid)
            {
                return datadogConfigResult.Result;
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
        public T? GetAs<T>(Func<DefaultResult<T>>? getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter)
        {
            var result = GetAs(validator, converter);

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
            Telemetry.Record(Key, defaultValue.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
            return defaultValue.Result!;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public T? GetAs<T>(Func<DefaultResult<T>>? getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter, string openTelemetryKey, Func<string, ParsingResult<T>> openTelemetryConverter)
        {
            var datadogConfigResult = GetAs(validator, converter);

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            if (datadogConfigResult.IsPresent && Source.IsPresent(openTelemetryKey))
            {
                // TODO Log to user and report "otel.env.hiding" telemetry metric
            }
            else if (Source.IsPresent(openTelemetryKey))
            {
                var openTelemetryResult = Source.GetAs(openTelemetryKey, Telemetry, openTelemetryConverter, validator, recordValue: true); // replace with null telemetry

                if (openTelemetryResult is { Result: { } openTelemetryValue, IsValid: true })
                {
                    return openTelemetryValue;
                }
                else
                {
                    // TODO Log to user and report "otel.env.invalid" telemetry metric
                }
            }

            if (datadogConfigResult.IsValid)
            {
                return datadogConfigResult.Result;
            }

            // don't have a valid value
            if (getDefaultValue is null)
            {
                return default;
            }

            var defaultValue = getDefaultValue();
            Telemetry.Record(Key, defaultValue.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
            return defaultValue.Result!;
        }

        // ****************
        // Bool accessors
        // ****************
        public bool? AsBool() => AsBool(getDefaultValue: null, validator: null);

        public bool AsBool(bool defaultValue) => AsBool(() => defaultValue, validator: null).Value;

        public bool? AsBool(Func<bool, bool> validator) => AsBool(null, validator);

        public bool AsBool(bool defaultValue, Func<bool, bool>? validator)
            => AsBool(() => defaultValue, validator).Value;

        public bool AsBoolWithOpenTelemetryMapping(bool defaultValue, string openTelemetryKey, Func<string, ParsingResult<bool>>? openTelemetryConverter = null)
            => AsBool(() => defaultValue, validator: null, openTelemetryKey, openTelemetryConverter).Value;

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator)
            => AsBool(getDefaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator, Func<string, ParsingResult<bool>>? converter)
        {
            var result = GetBoolResult(validator, converter);

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

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator, string openTelemetryKey, Func<string, ParsingResult<bool>>? openTelemetryConverter = null)
        {
            var datadogConfigResult = GetResult(AsBoolSelector, validator, recordValue: true);

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            if (datadogConfigResult.IsPresent && Source.IsPresent(openTelemetryKey))
            {
                // TODO Log to user and report "otel.env.hiding" telemetry metric
            }
            else if (Source.IsPresent(openTelemetryKey))
            {
                var openTelemetryResult = openTelemetryConverter switch
                {
                    null => Source.GetBool(openTelemetryKey, Telemetry, validator),
                    _ => Source.GetAs(openTelemetryKey, Telemetry, openTelemetryConverter, validator, recordValue: true), // replace with null telemetry
                };

                if (openTelemetryResult is { Result: { } openTelemetryValue, IsValid: true })
                {
                    return openTelemetryValue;
                }
                else
                {
                    // TODO Log to user and report "otel.env.invalid" telemetry metric
                }
            }

            if (datadogConfigResult.IsValid)
            {
                return datadogConfigResult.Result;
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
            => AsInt32(defaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))] // This doesn't work with nullables, but it still expresses intent
        public int? AsInt32(int? defaultValue, Func<int, bool>? validator, Func<string, ParsingResult<int>>? converter)
        {
            var result = GetInt32Result(validator, converter);

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
            => AsDouble(defaultValue, validator, converter: null);

        [return: NotNullIfNotNull(nameof(defaultValue))]
        public double? AsDouble(double? defaultValue, Func<double, bool>? validator, Func<string, ParsingResult<double>>? converter)
        {
            var result = GetDoubleResult(validator, converter);

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

        public double? AsOpenTelemetrySampleRate()
        {
            var openTelemetryKey = ConfigurationKeys.OpenTelemetry.TracesSampler;
            var openTelemetryArgKey = ConfigurationKeys.OpenTelemetry.TracesSamplerArg;

            var datadogConfigResult = GetResult(AsDoubleSelector, validator: null, recordValue: true);

            double? returnValue = datadogConfigResult.IsValid
                                      ? datadogConfigResult.Result
                                      : null;

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            var samplerKeyPresent = Source.IsPresent(openTelemetryKey);
            var samplerArgKeyPresent = Source.IsPresent(openTelemetryArgKey);
            if (datadogConfigResult.IsPresent)
            {
                if (samplerKeyPresent)
                {
                    // TODO Log to user and report "otel.env.hiding" telemetry metric
                }

                if (samplerArgKeyPresent)
                {
                    // TODO Log to user and report "otel.env.hiding" telemetry metric
                }
            }
            else if (samplerKeyPresent)
            {
                var samplerResult = Source.GetString(openTelemetryKey, Telemetry, validator: null, recordValue: true);

                // Emit a telemetry warning that we saw both configurations
                if (samplerResult is { Result: { } samplerName, IsValid: true })
                {
                    string? supportedSamplerName = samplerName switch
                    {
                        "parentbased_always_on" => "parentbased_always_on",
                        "always_on" => "parentbased_always_on",
                        "parentbased_always_off" => "parentbased_always_off",
                        "always_off" => "parentbased_always_off",
                        "parentbased_traceidratio" => "parentbased_traceidratio",
                        "traceidratio" => "parentbased_traceidratio",
                        _ => null,
                    };

                    if (supportedSamplerName is null)
                    {
                        // TODO log warning that the OpenTelemetry value is invalid
                        return returnValue;
                    }
                    else if (!string.Equals(samplerName, supportedSamplerName, StringComparison.OrdinalIgnoreCase))
                    {
                        // TODO log warning that the configuration is not supported
                    }

                    var samplerArgResult = Source.GetDouble(openTelemetryArgKey, NullConfigurationTelemetry.Instance, validator: null);
                    ConfigurationResult<double>? openTelemetrySampleRateResult = supportedSamplerName switch
                    {
                        "parentbased_always_on" => ConfigurationResult<double>.Valid(1.0),
                        "parentbased_always_off" => ConfigurationResult<double>.Valid(0.0),
                        "parentbased_traceidratio" => samplerArgResult,
                        _ => null,
                    };

                    if (openTelemetrySampleRateResult is { Result: { } sampleRateResult, IsValid: true })
                    {
                        return sampleRateResult;
                    }
                    else
                    {
                        // TODO Log to user and report "otel.env.invalid" telemetry metric
                    }
                }
            }

            return returnValue;
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

            // We have a valid value
            if (result is { Result: { } value, IsValid: true })
            {
                return value;
            }

            if (getDefaultValue != null)
            {
                var defaultValue = getDefaultValue();
                Telemetry.Record(Key, defaultValue.TelemetryValue, true, ConfigurationOrigins.Default);
                return defaultValue.Result;
            }

            return null;
        }

        public IDictionary<string, string>? AsDictionaryWithOpenTelemetryMapping(string openTelemetryKey, Func<DefaultResult<IDictionary<string, string>>>? getDefaultValue = null)
        {
            // TODO: Handle/allow default values + validation?
            var result = GetDictionaryResult(allowOptionalMappings: false, separator: ':');

            IDictionary<string, string>? returnValue = null;
            bool datadogConfigurationIsPresent = false;
            if (result is { Result: { } value, IsValid: { } resultIsValid })
            {
                datadogConfigurationIsPresent = true;
                if (resultIsValid)
                {
                    returnValue = value;
                }
            }
            else
            {
                datadogConfigurationIsPresent = Source.IsPresent(Key)
                                             || (FallbackKey1 is null ? false : Source.IsPresent(FallbackKey1))
                                             || (FallbackKey2 is null ? false : Source.IsPresent(FallbackKey2))
                                             || (FallbackKey3 is null ? false : Source.IsPresent(FallbackKey3));
            }

            // OpenTelemetry key must always be checked so we can warn the user about the conflicting variables
            var openTelemetryResult = Source.GetDictionary(openTelemetryKey, NullConfigurationTelemetry.Instance, validator: null, allowOptionalMappings: false, separator: '=');

            // Emit a telemetry warning that we saw both configurations
            if (openTelemetryResult is { Result: { } openTelemetryValue, IsValid: { } openTelemetryResultIsValid })
            {
                if (datadogConfigurationIsPresent)
                {
                    // TODO emit telemetry warning that we saw both
                }
                else if (openTelemetryResultIsValid)
                {
                    // Update well-known service information resources
                    if (openTelemetryValue.TryGetValue("deployment.environment", out var envValue))
                    {
                        openTelemetryValue.Remove("deployment.environment");
                        openTelemetryValue.Add(Tags.Env, envValue);
                    }

                    if (openTelemetryValue.TryGetValue("service.name", out var serviceValue))
                    {
                        openTelemetryValue.Remove("service.name");
                        openTelemetryValue.Add("service", serviceValue);
                    }

                    if (openTelemetryValue.TryGetValue("service.version", out var versionValue))
                    {
                        openTelemetryValue.Remove("service.version");
                        openTelemetryValue.Add(Tags.Version, versionValue);
                    }

                    // TODO emit telemetry success
                    return openTelemetryValue;
                }
            }

            if (returnValue is not null)
            {
                return returnValue;
            }

            if (getDefaultValue != null)
            {
                var defaultValue = getDefaultValue();
                Telemetry.Record(Key, defaultValue.TelemetryValue, true, ConfigurationOrigins.Default);
                return defaultValue.Result;
            }

            return null;
        }

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
}
