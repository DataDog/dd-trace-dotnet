// <copyright file="DirectSubmissionSerilogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission
{
    /// <summary>
    /// Serilog Sink
    /// </summary>
    internal class DirectSubmissionSerilogSink
    {
        private readonly IDirectSubmissionLogSink _sink;
        private readonly int _minimumLevel;
        private bool _isDisabled;

        internal DirectSubmissionSerilogSink(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel minimumLevel)
        {
            _sink = sink;
            _minimumLevel = (int)minimumLevel;
        }

        /// <summary>
        /// Emit the provided log event to the sink
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Serilog.Events.LogEvent, Serilog" })]
        public void Emit(ILogEvent? logEvent)
        {
            if (_isDisabled
                || logEvent is null
                || (int)logEvent.Level < _minimumLevel)
            {
                return;
            }

            TelemetryFactory.Metrics.RecordCountDirectLogLogs(MetricTags.IntegrationName.Serilog);
            _sink.EnqueueLog(new SerilogDirectSubmissionLogEvent(logEvent));
        }

        internal void Disable()
        {
            _isDisabled = true;
        }
    }
}
