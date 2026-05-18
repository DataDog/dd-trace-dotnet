// <copyright file="DictionaryObjectConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Util;

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

    public ConfigurationResult<string> GetString(string key, Func<string, bool>? validator, bool recordValue)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not string value)
            {
                return ConfigurationResult<string>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                return ConfigurationResult<string>.Valid(value);
            }

            return ConfigurationResult<string>.Invalid(value);
        }

        return ConfigurationResult<string>.NotFound();
    }

    public ConfigurationResult<int> GetInt32(string key, Func<int, bool>? validator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not int value)
            {
                return ConfigurationResult<int>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                return ConfigurationResult<int>.Valid(value);
            }

            return ConfigurationResult<int>.Invalid(value);
        }

        return ConfigurationResult<int>.NotFound();
    }

    public ConfigurationResult<double> GetDouble(string key, Func<double, bool>? validator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not double value)
            {
                return ConfigurationResult<double>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                return ConfigurationResult<double>.Valid(value);
            }

            return ConfigurationResult<double>.Invalid(value);
        }

        return ConfigurationResult<double>.NotFound();
    }

    public ConfigurationResult<bool> GetBool(string key, Func<bool, bool>? validator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not bool value)
            {
                return ConfigurationResult<bool>.ParseFailure();
            }

            if (validator is null || validator(value))
            {
                return ConfigurationResult<bool>.Valid(value);
            }

            return ConfigurationResult<bool>.Invalid(value);
        }

        return ConfigurationResult<bool>.NotFound();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator)
        => GetDictionary(key, validator, allowOptionalMappings: false, separator: ':');

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
    {
        if (TryGetValue(key, out var objValue) && objValue is not null)
        {
            if (objValue is not IDictionary<string, string> value)
            {
                return ConfigurationResult<IDictionary<string, string>>.ParseFailure();
            }

            var dictAsString = string.Empty;
            if (value.Count > 0)
            {
                var sb = StringBuilderCache.Acquire();
                foreach (var kvp in value)
                {
                    sb.Append(kvp.Key)
                      .Append(':')
                      .Append(kvp.Value)
                      .Append(separator);
                }

                // remove the final separator (we know there was at least one so this is safe)
                sb.Remove(sb.Length - 1, 1);
                dictAsString = StringBuilderCache.GetStringAndRelease(sb);
            }

            if (validator is null || validator(value))
            {
                return ConfigurationResult<IDictionary<string, string>>.Valid(value, dictAsString);
            }

            return ConfigurationResult<IDictionary<string, string>>.Invalid(value);
        }

        return ConfigurationResult<IDictionary<string, string>>.NotFound();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
        => GetDictionary(key, validator, allowOptionalMappings: false, separator: ':');

    public ConfigurationResult<T> GetAs<T>(string key, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
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
                    return ConfigurationResult<T>.Valid(result.Result, valueAsString);
                }

                return ConfigurationResult<T>.Invalid(result.Result);
            }

            return ConfigurationResult<T>.ParseFailure();
        }

        return ConfigurationResult<T>.NotFound();
    }
}
