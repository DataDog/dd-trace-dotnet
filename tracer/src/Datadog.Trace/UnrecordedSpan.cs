// <copyright file="UnrecordedSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.LibDatadog.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace;

internal sealed class UnrecordedSpan : SpanBase
{
    private readonly UnrecordedSpanContext _context;
    private int _isFinished;

    internal UnrecordedSpan(UnrecordedSpanContext context)
    {
        _context = context;
    }

    internal override SpanContext Context => _context.Context;

    /// <summary>
    /// Gets the resource name
    /// </summary>
    internal override string? ResourceName => _context.ResourceName;

    /// <summary>
    /// Gets the operation name
    /// </summary>
    internal string? OperationName => _context.OperationName;

    internal override void Finish() => Finish(TimeSpan.Zero);

    internal override void Finish(DateTimeOffset finishTimestamp)
        => Finish(TimeSpan.Zero);

    internal override void Finish(TimeSpan duration)
    {
        if (Interlocked.CompareExchange(ref _isFinished, 1, 0) == 0)
        {
            Context.TraceContext.CloseSpan(this);

            TelemetryFactory.Metrics.RecordCountSpanFinished();
        }
    }
}
