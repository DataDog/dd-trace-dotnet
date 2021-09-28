// <copyright file="LogEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting
{
    internal readonly struct LogEntry<TState>
    {
        public LogEntry(
            DateTime timestamp,
            int logLevel,
            string category,
            int eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter,
            IExternalScopeProvider scopeProvider)
        {
            Timestamp = timestamp;
            LogLevel = logLevel;
            Category = category;
            EventId = eventId;
            State = state;
            Exception = exception;
            Formatter = formatter;
            ScopeProvider = scopeProvider;
        }

        public DateTime Timestamp { get; }

        public int LogLevel { get; }

        public string Category { get; }

        public int EventId { get; }

        public TState State { get; }

        public Exception Exception { get; }

        public Func<TState, Exception, string> Formatter { get; }

        public IExternalScopeProvider ScopeProvider { get; }
    }
}
