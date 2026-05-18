// <copyright file="NullConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

internal sealed class NullConfigurationSource : IConfigurationSource
{
    public static readonly NullConfigurationSource Instance = new();

    public ConfigurationOrigins Origin => ConfigurationOrigins.Unknown;

    public ConfigurationResult<string> GetString(string key, Func<string, bool>? validator, bool recordValue)
        => ConfigurationResult<string>.NotFound();

    public ConfigurationResult<int> GetInt32(string key, Func<int, bool>? validator)
        => ConfigurationResult<int>.NotFound();

    public ConfigurationResult<double> GetDouble(string key, Func<double, bool>? validator)
        => ConfigurationResult<double>.NotFound();

    public ConfigurationResult<bool> GetBool(string key, Func<bool, bool>? validator)
        => ConfigurationResult<bool>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator)
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<T> GetAs<T>(string key, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        => ConfigurationResult<T>.NotFound();
}
