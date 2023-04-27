// <copyright file="IConfigurationTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Configuration.Telemetry;

internal interface IConfigurationTelemetry
{
    void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null);

    void Record(string key, bool value, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null);

    void Record(string key, double value, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null);

    void Record(string key, int value, ConfigurationOrigins origin, ConfigurationTelemetryErrorCode? error = null);
}
