// <copyright file="TelemetryLoggingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging.Internal.Configuration;

internal class TelemetryLoggingConfiguration
{
    public TelemetryLoggingConfiguration(int bufferSize, int batchSize, TimeSpan flushPeriod)
    {
        BufferSize = bufferSize;
        BatchSize = batchSize;
        FlushPeriod = flushPeriod;
        LogLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Warning);
        TelemetrySink = new(CreateTelemetrySink);
    }

    /// <summary>
    /// Gets the total number of logs to keep before we start dropping
    /// </summary>
    public int BufferSize { get; }

    /// <summary>
    /// Gets the number of logs to include in a single telemetry batch
    /// </summary>
    public int BatchSize { get; }

    /// <summary>
    /// Gets how often do we flush all queued logs
    /// </summary>
    public TimeSpan FlushPeriod { get; }

    /// <summary>
    /// Gets a switch to stop sending logs to the telemetry sink
    /// </summary>
    public LoggingLevelSwitch LogLevelSwitch { get; }

    /// <summary>
    /// Gets or creates the <see cref="TelemetryLogsSink"/> used to send logs, based on the configuration
    /// </summary>
    public Lazy<TelemetryLogsSink> TelemetrySink { get; }

    private TelemetryLogsSink CreateTelemetrySink()
    {
        var batchingOptions = new BatchingSinkOptions(
            batchSizeLimit: BufferSize,
            queueLimit: BatchSize,
            period: FlushPeriod);

        // If the sink gets disabled, switch to Fatal log level, so it's
        // not sent any more logs. Also, use the null logger instance in
        // the sink so we don't get into a weird continuous loop!
        return new TelemetryLogsSink(
            batchingOptions,
            () => LogLevelSwitch.MinimumLevel = LogEventLevel.Fatal,
            DatadogSerilogLogger.NullLogger,
            deDuplicationEnabled: true); // This sends only messageTemplates, so may as wel dedupe
    }
}
