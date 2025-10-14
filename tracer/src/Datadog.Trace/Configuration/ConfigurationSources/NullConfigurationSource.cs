// <copyright file="NullConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.ConfigurationSources.Registry;
using Datadog.Trace.Configuration.ConfigurationSources.Registry.Generated;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

internal class NullConfigurationSource : IConfigurationSource
{
    public static readonly NullConfigurationSource Instance = new();

    public ConfigurationOrigins Origin => ConfigurationOrigins.Unknown;

    public ConfigurationResult<string> GetString<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        where TKey : struct, IConfigKey
        => ConfigurationResult<string>.NotFound();

    public ConfigurationResult<int> GetInt32<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        where TKey : struct, IConfigKey
        => ConfigurationResult<int>.NotFound();

    public ConfigurationResult<double> GetDouble<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        where TKey : struct, IConfigKey
        => ConfigurationResult<double>.NotFound();

    public ConfigurationResult<bool> GetBool<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        where TKey : struct, IConfigKey
        => ConfigurationResult<bool>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
        where TKey : struct, IConfigKey
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
        where TKey : struct, IConfigKey
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary<TKey>(TKey key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, Func<string, IDictionary<string, string>> parser)
        where TKey : struct, IConfigKey
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<T> GetAs<TKey, T>(TKey key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        where TKey : struct, IConfigKey
        => ConfigurationResult<T>.NotFound();
}
