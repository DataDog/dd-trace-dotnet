// <copyright file="DiagnosticTelemetryLoggingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging.Internal.Configuration;

internal class DiagnosticTelemetryLoggingConfiguration
{
    /// <summary>
    /// The maximum amount of time diagnostic telemetry should be enabled for
    /// </summary>
    public const int MaximumDurationSeconds = 60 * 60 * 4; // 4 hours

    public DiagnosticTelemetryLoggingConfiguration(DateTimeOffset disableAt)
    {
        LogLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        DisableAt = disableAt;
    }

    /// <summary>
    /// Gets the time at which diagnostic telemetry logs should be disabled
    /// </summary>
    public DateTimeOffset DisableAt { get; }

    /// <summary>
    /// Gets a switch to stop sending logs to the telemetry sink
    /// </summary>
    public LoggingLevelSwitch LogLevelSwitch { get; }
}
