// <copyright file="CustomTelemeteredConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration;

internal class CustomTelemeteredConfigurationSource : ITelemeteredConfigurationSource
{
    public CustomTelemeteredConfigurationSource(IConfigurationSource source)
    {
        // Not strictly a public API, but tells us how often people are creating custom IConfigurationSource implementations
        TelemetryFactory.Metrics.Record(PublicApiUsage.CustomTelemeteredConfigurationSource_Ctor);
        Source = source;
    }

    public IConfigurationSource Source { get; }

    bool ITelemeteredConfigurationSource.IsPresent(string key)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var result = Source.GetString(key);
#pragma warning restore DD0002

        return result is not null;
    }

    public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var result = Source.GetString(key);
#pragma warning restore DD0002
        if (result is null)
        {
            return ConfigurationResult<string>.NotFound();
        }

        if (validator is null || validator(result))
        {
            telemetry.Record(key, result, recordValue, ConfigurationOrigins.Code);
            return ConfigurationResult<string>.Valid(result);
        }

        telemetry.Record(key, result, recordValue, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation);
        return ConfigurationResult<string>.Invalid(result);
    }

    public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var result = Source.GetInt32(key);
#pragma warning restore DD0002
        if (result is null)
        {
            // Because this is an IConfigurationSource _not_ an ITelemeteredConfigurationSource
            // we can't distinguish between "not found" and "found but failed to parse"
            // so we just have to accept this for now. Alternatively, we could call `GetString()`
            // and do the parsing ourselves, but that doesn't necessarily have the same behaviour
            // (e.g. this would fail with a json-based source)
            return ConfigurationResult<int>.NotFound();
        }

        if (validator is null || validator(result.Value))
        {
            telemetry.Record(key, result.Value, ConfigurationOrigins.Code);
            return ConfigurationResult<int>.Valid(result.Value);
        }

        telemetry.Record(key, result.Value, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation);
        return ConfigurationResult<int>.Invalid(result.Value);
    }

    public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var result = Source.GetDouble(key);
#pragma warning restore DD0002
        if (result is null)
        {
            // Because this is an IConfigurationSource _not_ an ITelemeteredConfigurationSource
            // we can't distinguish between "not found" and "found but failed to parse"
            // so we just have to accept this for now. Alternatively, we could call `GetString()`
            // and do the parsing ourselves, but that doesn't necessarily have the same behaviour
            // (e.g. this would fail with a json-based source)
            return ConfigurationResult<double>.NotFound();
        }

        if (validator is null || validator(result.Value))
        {
            telemetry.Record(key, result.Value, ConfigurationOrigins.Code);
            return ConfigurationResult<double>.Valid(result.Value);
        }

        telemetry.Record(key, result.Value, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation);
        return ConfigurationResult<double>.Invalid(result.Value);
    }

    public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var result = Source.GetBool(key);
#pragma warning restore DD0002
        if (result is null)
        {
            // Because this is an IConfigurationSource _not_ an ITelemeteredConfigurationSource
            // we can't distinguish between "not found" and "found but failed to parse"
            // so we just have to accept this for now. Alternatively, we could call `GetString()`
            // and do the parsing ourselves, but that doesn't necessarily have the same behaviour
            // (e.g. this would fail with a json-based source)
            return ConfigurationResult<bool>.NotFound();
        }

        if (validator is null || validator(result.Value))
        {
            telemetry.Record(key, result.Value, ConfigurationOrigins.Code);
            return ConfigurationResult<bool>.Valid(result.Value);
        }

        telemetry.Record(key, result.Value, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation);
        return ConfigurationResult<bool>.Invalid(result.Value);
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
        => GetDictionary(key, telemetry, validator, allowOptionalMappings: false, separator: ':');

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var result = Source.GetDictionary(key, allowOptionalMappings);
#pragma warning restore DD0002
        if (result is null)
        {
            // Because this is an IConfigurationSource _not_ an ITelemeteredConfigurationSource
            // we can't distinguish between "not found" and "found but failed to parse"
            // so we just have to accept this for now. Alternatively, we could call `GetString()`
            // and do the parsing ourselves, but that doesn't necessarily have the same behaviour
            // (e.g. this would fail with a json-based source)
            return ConfigurationResult<IDictionary<string, string>>.NotFound();
        }

        // This is horrible. We _could_ call Source.GetString(), but as this is a custom implementation,
        // there's no reason that has to give back the "raw" dictionary value, so this is probably th best we can do
        string stringifiedDictionary;
        if (result.Count > 0)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            foreach (var kvp in result)
            {
                sb.Append(kvp.Key).Append(':').Append(kvp.Value).Append(',');
            }

            // Remove trailing comma
            stringifiedDictionary = sb.ToString(0, length: sb.Length - 1);
            StringBuilderCache.Release(sb);
        }
        else
        {
            stringifiedDictionary = string.Empty;
        }

        if (validator is null || validator(result))
        {
            telemetry.Record(key, stringifiedDictionary, recordValue: true, ConfigurationOrigins.Code);
            return ConfigurationResult<IDictionary<string, string>>.Valid(result);
        }

        telemetry.Record(key, stringifiedDictionary, recordValue: true, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation);
        return ConfigurationResult<IDictionary<string, string>>.Invalid(result);
    }

    public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
    {
#pragma warning disable DD0002 // This class is intentionally a wrapper around IConfigurationSource
        var value = Source.GetString(key);
#pragma warning restore DD0002

        if (value is null)
        {
            // Because this is an IConfigurationSource _not_ an ITelemeteredConfigurationSource
            // we can't distinguish between "not found" and "found but failed to parse"
            // so we just have to accept this for now. Alternatively, we could call `GetString()`
            // and do the parsing ourselves, but that doesn't necessarily have the same behaviour
            // (e.g. this would fail with a json-based source)
            return ConfigurationResult<T>.NotFound();
        }

        var result = converter(value);
        if (result.IsValid)
        {
            if (validator is null || validator(result.Result))
            {
                telemetry.Record(key, value, recordValue, ConfigurationOrigins.Code);
                return ConfigurationResult<T>.Valid(result.Result);
            }

            telemetry.Record(key, value, recordValue, ConfigurationOrigins.Code, TelemetryErrorCode.FailedValidation);
            return ConfigurationResult<T>.Invalid(result.Result);
        }

        telemetry.Record(key, value, recordValue, ConfigurationOrigins.Code, TelemetryErrorCode.ParsingCustomError);
        return ConfigurationResult<T>.ParseFailure();
    }
}
