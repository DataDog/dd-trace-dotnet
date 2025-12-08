// <copyright file="DatadogLogEventCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Formatting;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;

/// <summary>
/// Creates log events in Datadog format
/// </summary>
internal class DatadogLogEventCreator : ILogEventCreator
{
    private readonly LogFormatter _logFormatter;
    private readonly IExternalScopeProvider? _scopeProvider;

    public DatadogLogEventCreator(LogFormatter logFormatter, IExternalScopeProvider? scopeProvider)
    {
        _logFormatter = logFormatter;
        _scopeProvider = scopeProvider;
    }

    public LoggerDirectSubmissionLogEvent CreateLogEvent<TState>(
        int logLevel,
        string categoryName,
        object eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var logEntry = new LogEntry<TState>(
            DateTime.UtcNow,
            logLevel,
            categoryName,
            eventId.GetHashCode(),
            state,
            exception,
            formatter,
            _scopeProvider);

        var serializedLog = LoggerLogFormatter.FormatLogEvent(_logFormatter, logEntry);
        return new LoggerDirectSubmissionLogEvent(serializedLog);
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return _scopeProvider?.Push(state) ?? NullDisposable.Instance;
    }

    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
