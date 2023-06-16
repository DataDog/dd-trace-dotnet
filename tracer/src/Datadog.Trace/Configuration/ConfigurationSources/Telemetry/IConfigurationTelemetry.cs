// <copyright file="IConfigurationTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

internal interface IConfigurationTelemetry
{
    void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    void Record(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    void Record(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    void Record(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    void Record(string key, double? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    void Record(string key, int? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    public ICollection<ConfigurationKeyValue>? GetData();

    /// <summary>
    /// Copies the stored configuration to the provided destination
    /// Note that this should not remove the configuration elements from the source
    /// </summary>
    public void CopyTo(IConfigurationTelemetry destination);
}
