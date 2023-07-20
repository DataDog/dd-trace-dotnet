// <copyright file="DirectSubmissionNLogTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission
{
    /// <summary>
    /// NLog Target that sends logs directly to Datadog
    /// </summary>
    internal class DirectSubmissionNLogTarget
    {
        private readonly IDirectSubmissionLogSink _sink;
        private readonly int _minimumLevel;
        private readonly LogFormatter? _formatter;
        private ITargetWithContextBaseProxy? _baseProxy;

        internal DirectSubmissionNLogTarget(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel minimumLevel)
            : this(sink, minimumLevel, formatter: null)
        {
        }

        // internal for testing
        internal DirectSubmissionNLogTarget(
            IDirectSubmissionLogSink sink,
            DirectSubmissionLogLevel minimumLevel,
            LogFormatter? formatter)
        {
            _sink = sink;
            _formatter = formatter;
            _minimumLevel = (int)minimumLevel;
        }

        /// <summary>
        /// Writes logging event to the log target
        /// </summary>
        /// <param name="logEventInfo">Logging event to be written out</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "NLog.LogEventInfo, NLog" })]
        public void Write(ILogEventInfoProxy? logEventInfo)
        {
            if (logEventInfo is null)
            {
                return;
            }

            if (logEventInfo.Level.Ordinal < _minimumLevel)
            {
                return;
            }

            // Nlog automatically includes all the properties from the event, so don't need fallback properties
            var mappedProperties = _baseProxy?.GetAllProperties(logEventInfo);

            // We render the event to a string immediately as we need to capture the properties
            // This is more expensive from a CPU perspective, but is necessary as the properties
            // won't necessarily be serialized correctly otherwise (e.g. dd_span_id/dd_trace_id)

            var logEvent = new LogEntry(logEventInfo, mappedProperties, fallbackProperties: null);
            var logFormatter = _formatter ?? TracerManager.Instance.DirectLogSubmission.Formatter;
            var serializedLog = NLogLogFormatter.FormatLogEvent(logFormatter, logEvent);

            TelemetryFactory.Metrics.RecordCountDirectLogLogs(MetricTags.IntegrationName.NLog);
            _sink.EnqueueLog(new NLogDirectSubmissionLogEvent(serializedLog));
        }

        internal void SetBaseProxy(ITargetWithContextBaseProxy baseProxy)
        {
            _baseProxy = baseProxy;
            _baseProxy.IncludeEventProperties = true;
            _baseProxy.IncludeMdc = true;
            _baseProxy.IncludeMdlc = true;
            _baseProxy.IncludeNdc = true;
            _baseProxy.IncludeNdlc = true;
        }
    }
}
