// <copyright file="OtlpSubmissionLogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Logs;

namespace Datadog.Trace.Logging.DirectSubmission.Sink;

internal class OtlpSubmissionLogSink : BatchingSink<DirectSubmissionLogEvent>, IDirectSubmissionLogSink
{
    private readonly IDatadogLogger _logger = DatadogLogging.GetLoggerFor<OtlpSubmissionLogSink>();
    private readonly IOtlpExporter _otlpExporter;

    public OtlpSubmissionLogSink(BatchingSinkOptions sinkOptions, TracerSettings settings)
        : this(sinkOptions, new OtlpExporter(settings))
    {
    }

    internal OtlpSubmissionLogSink(BatchingSinkOptions sinkOptions, IOtlpExporter exporter)
        : base(sinkOptions, null)
    {
        _otlpExporter = exporter;
    }

    protected override async Task<bool> EmitBatch(Queue<DirectSubmissionLogEvent> events)
    {
        if (events.Count == 0)
        {
            return true;
        }

        try
        {
            var logRecords = new List<LogPoint>();
            foreach (var ev in events)
            {
                if (ev is LoggerDirectSubmissionLogEvent { OtlpLog: { } logPoint })
                {
                    logRecords.Add(logPoint);
                }
            }

            if (logRecords.Count == 0)
            {
                return true;
            }

            var result = await _otlpExporter.ExportAsync(logRecords).ConfigureAwait(false);
            return result == ExportResult.Success;
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error occurred sending OTLP logs.");
            return false;
        }
    }

    protected override void FlushingEvents(int queueSizeBeforeFlush)
    {
    }

    protected override void DelayEvents(TimeSpan delayUntilNextFlush)
    {
    }

    public override async Task DisposeAsync()
    {
        await DisposeAsync(true).ConfigureAwait(false);
    }
}
#endif
