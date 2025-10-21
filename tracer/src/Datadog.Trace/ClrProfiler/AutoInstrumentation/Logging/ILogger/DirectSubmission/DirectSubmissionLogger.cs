// <copyright file="DirectSubmissionLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
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
        private readonly IDirectSubmissionLogSink _sink;
        private readonly ILogEventCreator _logEventCreator;
        private readonly int _minimumLogLevel;

        internal DirectSubmissionLogger(
            string name,
            IDirectSubmissionLogSink sink,
            ILogEventCreator logEventCreator,
            DirectSubmissionLogLevel minimumLogLevel)
        {
            _name = name;
            _sink = sink;
            _logEventCreator = logEventCreator;
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

            var log = _logEventCreator.CreateLogEvent(logLevel, _name, eventId, state, exception, formatter);
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
        public IDisposable BeginScope<TState>(TState state) => _logEventCreator.BeginScope(state);
    }
}
