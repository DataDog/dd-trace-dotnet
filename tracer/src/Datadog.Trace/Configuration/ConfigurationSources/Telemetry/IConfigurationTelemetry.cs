// <copyright file="IConfigurationTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

/// <summary>
/// Used to collect configuration telemetry
/// </summary>
public interface IConfigurationTelemetry
{
    /// <summary>
    /// Records a value in telemetry, and marks it as the "current" value
    /// </summary>
    /// <param name="key">The key of the configuration</param>
    /// <param name="value">The value of the configuration</param>
    /// <param name="recordValue">Should the value be recorded, or redacted?</param>
    /// <param name="origin">Which configuration source the value came from</param>
    /// <param name="error">An optional error code</param>
    void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    /// <summary>
    /// Records a value in telemetry, and marks it as the "current" value
    /// </summary>
    /// <param name="key">The key of the configuration</param>
    /// <param name="value">The value of the configuration</param>
    /// <param name="origin">Which configuration source the value came from</param>
    /// <param name="error">An optional error code</param>
    void Record(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    /// <summary>
    /// Records a value in telemetry, and marks it as the "current" value
    /// </summary>
    /// <param name="key">The key of the configuration</param>
    /// <param name="value">The value of the configuration</param>
    /// <param name="origin">Which configuration source the value came from</param>
    /// <param name="error">An optional error code</param>
    void Record(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    /// <summary>
    /// Records a value in telemetry, and marks it as the "current" value
    /// </summary>
    /// <param name="key">The key of the configuration</param>
    /// <param name="value">The value of the configuration</param>
    /// <param name="origin">Which configuration source the value came from</param>
    /// <param name="error">An optional error code</param>
    void Record(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    /// <summary>
    /// Records a value in telemetry, and marks it as the "current" value
    /// </summary>
    /// <param name="key">The key of the configuration</param>
    /// <param name="value">The value of the configuration</param>
    /// <param name="origin">Which configuration source the value came from</param>
    /// <param name="error">An optional error code</param>
    void Record(string key, double? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    /// <summary>
    /// Records a value in telemetry, and marks it as the "current" value
    /// </summary>
    /// <param name="key">The key of the configuration</param>
    /// <param name="value">The value of the configuration</param>
    /// <param name="origin">Which configuration source the value came from</param>
    /// <param name="error">An optional error code</param>
    void Record(string key, int? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null);

    /// <summary>
    /// Gets the stored configuration data and clears the stored data
    /// </summary>
    /// <returns>The stored configuration data</returns>
    public ICollection<ConfigurationKeyValue>? GetData();

    /// <summary>
    /// Copies the stored configuration to the provided destination
    /// Note that this should not remove the configuration elements from the source
    /// </summary>
    /// <param name="destination">The destination for the copied data</param>
    public void CopyTo(IConfigurationTelemetry destination);

    /// <summary>
    /// Gets all the stored configuration data recorded so far
    /// </summary>
    ICollection<ConfigurationKeyValue>? GetFullData();
}
