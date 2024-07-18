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

internal class NullConfigurationSource : IConfigurationSource, ITelemeteredConfigurationSource
{
    public static readonly NullConfigurationSource Instance = new();

    public bool IsPresent(string key) => false;

    public string? GetString(string key) => null;

    public int? GetInt32(string key) => null;

    public double? GetDouble(string key) => null;

    public bool? GetBool(string key) => null;

    public IDictionary<string, string>? GetDictionary(string key) => null;

    public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings) => null;

    public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool>? validator, bool recordValue)
        => ConfigurationResult<string>.NotFound();

    public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator)
        => ConfigurationResult<int>.NotFound();

    public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator)
        => ConfigurationResult<double>.NotFound();

    public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator)
        => ConfigurationResult<bool>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator)
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator)
        => ConfigurationResult<IDictionary<string, string>>.NotFound();

    public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool>? validator, bool recordValue)
        => ConfigurationResult<T>.NotFound();
}
