// <copyright file="TelemetrySink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable

namespace Datadog.Trace.Logging.Internal.Sinks;

internal class TelemetrySink : ILogEventSink
{
    private readonly TelemetryLogsSink _sink;

    public TelemetrySink(TelemetryLogsSink sink)
    {
        _sink = sink;
    }

    // logs are immediately queued to channel
    public void Emit(LogEvent? logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        var stackTrace = logEvent.Exception is { } ex
                             ? StackTraceRedactor.Redact(ex.GetType().AssemblyQualifiedName ?? "Exception", new StackTrace(ex))
                             : null;

        // Note: we're using the raw message template here to remove any chance of including customer information
        var telemetryLog = new LogMessageData(logEvent.MessageTemplate.Text, ToLogLevel(logEvent.Level))
        {
            StackTrace = stackTrace
        };

        _sink.EnqueueLog(telemetryLog);
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
