// <copyright file="NullConfigurationTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

internal class NullConfigurationTelemetry : IConfigurationTelemetry
{
    public static readonly NullConfigurationTelemetry Instance = new();

    public void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
    }

    public void Record(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
    }

    public void Record(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
    }

    public void Record(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
    }

    public void Record(string key, double? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
    }

    public void Record(string key, int? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
    }

    public ICollection<ConfigurationKeyValue>? GetData() => null;

    public void CopyTo(IConfigurationTelemetry destination)
    {
    }
}
