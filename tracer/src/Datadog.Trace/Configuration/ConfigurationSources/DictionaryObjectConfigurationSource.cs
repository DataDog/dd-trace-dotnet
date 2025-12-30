// <copyright file="DictionaryObjectConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration;

internal class DictionaryObjectConfigurationSource : IConfigurationSource
{
    public DictionaryObjectConfigurationSource(IReadOnlyDictionary<string, object?> dictionary)
        : this(dictionary, ConfigurationOrigins.Code)
    {
    }

    public DictionaryObjectConfigurationSource(IReadOnlyDictionary<string, object?> dictionary, ConfigurationOrigins origin)
    {
        Dictionary = dictionary;
        Origin = origin;
    }

    public ConfigurationOrigins Origin { get; }

    protected IReadOnlyDictionary<string, object?> Dictionary { get; }

    protected virtual bool TryGetValue(string key, out object? value)
        => Dictionary.TryGetValue(key, out value);

    public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not string value)
            {
                telemetry.Record(key, objValue.ToString(), recordValue: true, Origin, TelemetryErrorCode.UnexpectedTypeInConfigurationSource);
                return ConfigurationResult<string>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                telemetry.Record(key, value, recordValue, Origin);
                return ConfigurationResult<string>.Valid(value);
            }

            telemetry.Record(key, value, recordValue, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<string>.Invalid(value);
        }

        return ConfigurationResult<string>.NotFound();
    }

    public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not int value)
            {
                telemetry.Record(key, objValue.ToString(), recordValue: true, Origin, TelemetryErrorCode.UnexpectedTypeInConfigurationSource);
                return ConfigurationResult<int>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                telemetry.Record(key, value, Origin);
                return ConfigurationResult<int>.Valid(value);
            }

            telemetry.Record(key, value, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<int>.Invalid(value);
        }

        return ConfigurationResult<int>.NotFound();
    }

    public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not double value)
            {
                telemetry.Record(key, objValue.ToString(), recordValue: true, Origin, TelemetryErrorCode.UnexpectedTypeInConfigurationSource);
                return ConfigurationResult<double>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                telemetry.Record(key, value, Origin);
                return ConfigurationResult<double>.Valid(value);
            }

            telemetry.Record(key, value, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<double>.Invalid(value);
        }

        return ConfigurationResult<double>.NotFound();
    }

    public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not bool value)
            {
                telemetry.Record(key, objValue.ToString(), recordValue: true, Origin, TelemetryErrorCode.UnexpectedTypeInConfigurationSource);
                return ConfigurationResult<bool>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                telemetry.Record(key, value, Origin);
                return ConfigurationResult<bool>.Valid(value);
            }

            telemetry.Record(key, value, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<bool>.Invalid(value);
        }

        return ConfigurationResult<bool>.NotFound();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
        => GetDictionary(key, telemetry, validator, allowOptionalMappings: false, separator: ':');

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not IDictionary<string, string> value)
            {
                telemetry.Record(key, objValue.ToString(), recordValue: true, Origin, TelemetryErrorCode.UnexpectedTypeInConfigurationSource);
                return ConfigurationResult<IDictionary<string, string>>.ParseFailure();
            }

#if NETCOREAPP
            var dictAsString = string.Join(separator, value.Select(x => $"{x.Key}:{x.Value}"));
#else
            var dictAsString = string.Join($"{separator}", value.Select(x => $"{x.Key}:{x.Value}"));
#endif
            if (validator is null || validator(value))
            {
                telemetry.Record(key, dictAsString, recordValue: true, Origin);
                return ConfigurationResult<IDictionary<string, string>>.Valid(value, dictAsString);
            }

            telemetry.Record(key, dictAsString, recordValue: true, Origin, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<IDictionary<string, string>>.Invalid(value);
        }

        return ConfigurationResult<IDictionary<string, string>>.NotFound();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
        => GetDictionary(key, telemetry, validator, allowOptionalMappings: false, separator: ':');

    public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            // Handle conversion
            var valueAsString = objValue.ToString()!;
            var result = objValue switch
            {
                T t => ParsingResult<T>.Success(t), // avoid the converter as we already have the value
                string s => converter(s),
                _ => converter(valueAsString),
            };

            if (result.IsValid)
            {
                if (validator is null || validator(result.Result))
                {
                    telemetry.Record(key, valueAsString, recordValue, Origin);
                    return ConfigurationResult<T>.Valid(result.Result, valueAsString);
                }

                telemetry.Record(key, valueAsString, recordValue, Origin, TelemetryErrorCode.FailedValidation);
                return ConfigurationResult<T>.Invalid(result.Result);
            }

            telemetry.Record(key, valueAsString, recordValue, Origin, TelemetryErrorCode.ParsingCustomError);
            return ConfigurationResult<T>.ParseFailure();
        }

        return ConfigurationResult<T>.NotFound();
    }
}
