// <copyright file="HandsOffConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Configuration.ConfigurationSources;

internal class HandsOffConfigurationSource : IConfigurationSource
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HandsOffConfigurationSource));

    public HandsOffConfigurationSource()
    {
        // read from libdatadog_conf
    }

    public bool IsPresent(string key)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<string> GetString(string key, IConfigurationTelemetry telemetry, Func<string, bool> validator, bool recordValue)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool> validator)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool> validator)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool> validator)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool> validator)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool> validator, bool allowOptionalMappings, char separator)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool> validator, Func<string, IDictionary<string, string>> parser)
    {
        throw new NotImplementedException();
    }

    public ConfigurationResult<T> GetAs<T>(string key, IConfigurationTelemetry telemetry, Func<string, ParsingResult<T>> converter, Func<T, bool> validator, bool recordValue)
    {
        throw new NotImplementedException();
    }
}
