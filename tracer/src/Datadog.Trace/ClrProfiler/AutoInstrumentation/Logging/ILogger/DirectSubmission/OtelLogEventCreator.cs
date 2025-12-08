// <copyright file="OtelLogEventCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;

/// <summary>
/// Creates log events in OTLP format
/// </summary>
internal class OtelLogEventCreator : ILogEventCreator
{
    public LoggerDirectSubmissionLogEvent CreateLogEvent<TState>(
        int logLevel,
        string categoryName,
        object eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        return OtlpLogEventBuilder.CreateLogEvent(logLevel, categoryName, eventId, state, exception, formatter);
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return NullDisposable.Instance;
    }

    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
#endif
