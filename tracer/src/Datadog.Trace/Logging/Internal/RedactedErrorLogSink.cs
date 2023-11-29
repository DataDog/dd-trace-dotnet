// <copyright file="RedactedErrorLogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging.Internal;

internal class RedactedErrorLogSink : ILogEventSink
{
    private readonly RedactedErrorLogCollector _collector;

    public RedactedErrorLogSink(RedactedErrorLogCollector collector)
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

        // Note: we're using the raw message template here to remove any chance of including customer information
        var stackTrace = logEvent.Exception is { } ex ? ExceptionRedactor.Redact(ex) : null;
        _collector.EnqueueLog(logEvent.MessageTemplate.Text, ToLogLevel(logEvent.Level), logEvent.Timestamp, stackTrace);
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
