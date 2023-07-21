// <copyright file="DirectSubmissionLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    /// <summary>
    /// An implementation of ILogger for use with direct log submission
    /// </summary>
    internal class DirectSubmissionLogger
    {
        private readonly string _name;
        private readonly IExternalScopeProvider? _scopeProvider;
        private readonly IDirectSubmissionLogSink _sink;
        private readonly LogFormatter? _logFormatter;
        private readonly int _minimumLogLevel;

        internal DirectSubmissionLogger(
            string name,
            IExternalScopeProvider? scopeProvider,
            IDirectSubmissionLogSink sink,
            LogFormatter? logFormatter,
            DirectSubmissionLogLevel minimumLogLevel)
        {
            _name = name;
            _scopeProvider = scopeProvider;
            _sink = sink;
            _logFormatter = logFormatter;
            _minimumLogLevel = (int)minimumLogLevel;
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written. Can be also an object.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a <see cref="string"/> message of the <paramref name="state"/> and <paramref name="exception"/>.</param>
        /// <typeparam name="TState">The type of the object to be written.</typeparam>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Microsoft.Extensions.Logging.LogLevel", "Microsoft.Extensions.Logging.EventId", "TState", "System.Exception", "Func`3" })]
        public void Log<TState>(int logLevel, object eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // We render the event to a string immediately as we need to capture the properties
            // This is more expensive from a CPU perspective, but saves having to persist the
            // properties to a dictionary and rendering later

            var logEntry = new LogEntry<TState>(
                DateTime.UtcNow,
                logLevel,
                _name,
                eventId.GetHashCode(),
                state,
                exception,
                formatter,
                _scopeProvider);
            var logFormatter = _logFormatter ?? TracerManager.Instance.DirectLogSubmission.Formatter;
            var serializedLog = LoggerLogFormatter.FormatLogEvent(logFormatter, logEntry);

            var log = new LoggerDirectSubmissionLogEvent(serializedLog);

            TelemetryFactory.Metrics.RecordCountDirectLogLogs(MetricTags.IntegrationName.ILogger);
            _sink.EnqueueLog(log);
        }

        /// <summary>
        /// Checks if the given <paramref name="logLevel"/> is enabled.
        /// </summary>
        /// <param name="logLevel">Level to be checked.</param>
        /// <returns><c>true</c> if enabled.</returns>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Microsoft.Extensions.Logging.LogLevel, Microsoft.Extensions.Logging.Abstractions" })]
        public bool IsEnabled(int logLevel) => logLevel >= _minimumLogLevel;

        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <param name="state">The identifier for the scope.</param>
        /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
        /// <returns>An <see cref="IDisposable"/> that ends the logical operation scope on dispose.</returns>
        [DuckReverseMethod(ParameterTypeNames = new[] { "TState" })]
        public IDisposable BeginScope<TState>(TState state) => _scopeProvider?.Push(state) ?? NullDisposable.Instance;

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
