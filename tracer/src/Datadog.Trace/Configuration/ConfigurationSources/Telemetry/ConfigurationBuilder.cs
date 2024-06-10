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

        /// <summary>
        /// Beware, this function won't record telemetry if the config isn't explicitly set.
        /// If you can, use <see cref="AsString(string)"/> instead or record telemetry manually.
        /// </summary>
        /// <returns>the string value of the configuration if set</returns>
        public string? AsString() => AsString(getDefaultValue: null, validator: null, recordValue: true);

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

        public string? AsStringWithOpenTelemetryMapping(string openTelemetryKey, Func<string, ParsingResult<string>>? openTelemetryConverter = null)
            => AsString(getDefaultValue: null, validator: null, recordValue: true, openTelemetryKey, openTelemetryConverter);

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
        private string? AsString(Func<string>? getDefaultValue, Func<string, bool>? validator, bool recordValue, string openTelemetryKey, Func<string, ParsingResult<string>>? openTelemetryConverter)
        {
            var result = Source.GetString(Key, Telemetry, validator, recordValue)
                      ?? (FallbackKey1 is null ? null : Source.GetString(FallbackKey1, Telemetry, validator, recordValue))
                      ?? (FallbackKey2 is null ? null : Source.GetString(FallbackKey2, Telemetry, validator, recordValue))
                      ?? (FallbackKey3 is null ? null : Source.GetString(FallbackKey3, Telemetry, validator, recordValue));

            string? returnValue = null;
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

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            if (datadogConfigurationIsPresent && Source.IsPresent(openTelemetryKey))
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

            if (returnValue is not null)
            {
                return returnValue;
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
            Telemetry.Record(Key, defaultValue.TelemetryValue, recordValue: true, ConfigurationOrigins.Default);
            return defaultValue.Result!;
        }

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public T? GetAs<T>(Func<DefaultResult<T>>? getDefaultValue, Func<T, bool>? validator, Func<string, ParsingResult<T>> converter, string openTelemetryKey, Func<string, ParsingResult<T>> openTelemetryConverter)
        {
            var result = Source.GetAs<T>(Key, Telemetry, converter, validator, recordValue: true)
                      ?? (FallbackKey1 is null ? null : Source.GetAs<T>(FallbackKey1, Telemetry, converter, validator, recordValue: true))
                      ?? (FallbackKey2 is null ? null : Source.GetAs<T>(FallbackKey2, Telemetry, converter, validator, recordValue: true))
                      ?? (FallbackKey3 is null ? null : Source.GetAs<T>(FallbackKey3, Telemetry, converter, validator, recordValue: true));

            T? returnValue = default;
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

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            if (datadogConfigurationIsPresent && Source.IsPresent(openTelemetryKey))
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

            if (result is { Result: { } datadogValue, IsValid: true })
            {
                return datadogValue;
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

        [return: NotNullIfNotNull(nameof(getDefaultValue))] // This doesn't work with nullables, but it still expresses intent
        public bool? AsBool(Func<bool>? getDefaultValue, Func<bool, bool>? validator, string openTelemetryKey, Func<string, ParsingResult<bool>>? openTelemetryConverter = null)
        {
            var result = Source.GetBool(Key, Telemetry, validator)
                      ?? (FallbackKey1 is null ? null : Source.GetBool(FallbackKey1, Telemetry, validator))
                      ?? (FallbackKey2 is null ? null : Source.GetBool(FallbackKey2, Telemetry, validator))
                      ?? (FallbackKey3 is null ? null : Source.GetBool(FallbackKey3, Telemetry, validator));

            bool? returnValue = null;
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

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            if (datadogConfigurationIsPresent && Source.IsPresent(openTelemetryKey))
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

            if (returnValue is not null)
            {
                return returnValue;
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

        public double? AsOpenTelemetrySampleRate()
        {
            var openTelemetryKey = ConfigurationKeys.OpenTelemetry.TracesSampler;
            var openTelemetryArgKey = ConfigurationKeys.OpenTelemetry.TracesSamplerArg;
            var result = Source.GetDouble(Key, Telemetry, validator: null)
                      ?? (FallbackKey1 is null ? null : Source.GetDouble(FallbackKey1, Telemetry, validator: null))
                      ?? (FallbackKey2 is null ? null : Source.GetDouble(FallbackKey2, Telemetry, validator: null))
                      ?? (FallbackKey3 is null ? null : Source.GetDouble(FallbackKey3, Telemetry, validator: null));

            double? returnValue = null;
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

            // If there's a Datadog configuration present, check if a corresponding OpenTelemetry key is present so we can log the conflicting keys
            var samplerKeyPresent = Source.IsPresent(openTelemetryKey);
            var samplerArgKeyPresent = Source.IsPresent(openTelemetryArgKey);
            if (datadogConfigurationIsPresent)
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
        public IDictionary<string, string>? AsDictionary(Func<IDictionary<string, string>>? getDefaultValue = null) => AsDictionary(allowOptionalMappings: false, getDefaultValue: getDefaultValue);

        [return: NotNullIfNotNull(nameof(getDefaultValue))]
        public IDictionary<string, string>? AsDictionary(bool allowOptionalMappings, Func<IDictionary<string, string>>? getDefaultValue = null)
        {
            // TODO: Handle/allow default values + validation?
            var result = Source.GetDictionary(Key, Telemetry, validator: null, allowOptionalMappings, separator: ':')
                      ?? (FallbackKey1 is null ? null : Source.GetDictionary(FallbackKey1, Telemetry, validator: null, allowOptionalMappings, separator: ':'))
                      ?? (FallbackKey2 is null ? null : Source.GetDictionary(FallbackKey2, Telemetry, validator: null, allowOptionalMappings, separator: ':'))
                      ?? (FallbackKey3 is null ? null : Source.GetDictionary(FallbackKey3, Telemetry, validator: null, allowOptionalMappings, separator: ':'));

            // We have a valid value
            if (result is { Result: { } value, IsValid: true })
            {
                return value;
            }

            if (getDefaultValue != null)
            {
                // Horrible that we have to stringify the dictionary, but that's all that's available in the telemetry api
                var defaultValue = getDefaultValue();
                var defaultValueAsString = defaultValue.Count == 0 ? string.Empty : string.Join(", ", defaultValue!.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

                Telemetry.Record(Key, defaultValueAsString, true, ConfigurationOrigins.Default);
                return defaultValue;
            }

            return null;
        }

        public IDictionary<string, string>? AsDictionaryWithOpenTelemetryMapping(string openTelemetryKey, Func<IDictionary<string, string>>? getDefaultValue = null)
        {
            // TODO: Handle/allow default values + validation?
            var result = Source.GetDictionary(Key, Telemetry, validator: null)
                      ?? (FallbackKey1 is null ? null : Source.GetDictionary(FallbackKey1, Telemetry, validator: null))
                      ?? (FallbackKey2 is null ? null : Source.GetDictionary(FallbackKey2, Telemetry, validator: null))
                      ?? (FallbackKey3 is null ? null : Source.GetDictionary(FallbackKey3, Telemetry, validator: null));

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
                // Horrible that we have to stringify the dictionary, but that's all that's available in the telemetry api
                var defaultValue = getDefaultValue();
                var defaultValueAsString = defaultValue.Count == 0 ? string.Empty : string.Join(", ", defaultValue!.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

                Telemetry.Record(Key, defaultValueAsString, true, ConfigurationOrigins.Default);
                return defaultValue;
            }

            return null;
        }
    }
}
