// <copyright file="DiagnosticTelemetryLogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging;

internal class DiagnosticTelemetryLogSink : ILogEventSink
{
    private readonly DiagnosticLogCollector _collector;

    public DiagnosticTelemetryLogSink(DiagnosticLogCollector collector)
    {
        _collector = collector;
    }

    // logs are immediately queued to channel
    public void Emit(LogEvent? logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        var message = logEvent.Exception is { } ex
                          ? $"{logEvent.MessageTemplate.Render(logEvent.Properties)}. Ex: {ex.Message}"
                          : logEvent.MessageTemplate.Render(logEvent.Properties);

        var logLevel = ToLogLevel(logEvent.Level);
        var telemetryLog = new DiagnosticLogMessageData(message, logLevel, logEvent.Timestamp) { StackTrace = logEvent.Exception?.StackTrace };

        _collector.EnqueueLog(telemetryLog);
    }

    private static TelemetryLogLevel ToLogLevel(LogEventLevel logEventLevel)
        => logEventLevel switch
        {
            LogEventLevel.Fatal => TelemetryLogLevel.ERROR,
            LogEventLevel.Error => TelemetryLogLevel.ERROR,
            LogEventLevel.Warning => TelemetryLogLevel.WARN,
            _ => TelemetryLogLevel.DEBUG,
        };
}
