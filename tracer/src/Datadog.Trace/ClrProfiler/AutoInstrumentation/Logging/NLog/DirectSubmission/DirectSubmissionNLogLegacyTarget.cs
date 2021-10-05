// <copyright file="DirectSubmissionNLogLegacyTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission
{
    /// <summary>
    /// NLog Target that sends logs directly to Datadog for NLog &lt;4.5
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DirectSubmissionNLogLegacyTarget
    {
        private readonly IDatadogSink _sink;
        private readonly int? _minimumLevel;
        private readonly LogFormatter _formatter;
        private Func<IDictionary<string, object>> _getProperties = null;

        internal DirectSubmissionNLogLegacyTarget()
        {
        }

        internal DirectSubmissionNLogLegacyTarget(
            IDatadogSink sink,
            DirectSubmissionLogLevel? minimumLevel,
            LogFormatter formatter)
        {
            _sink = sink;
            _formatter = formatter;
            _minimumLevel = (int?)minimumLevel;
        }

        /// <summary>
        /// Writes logging event to the log target
        /// </summary>
        /// <param name="logEventInfo">Logging event to be written out</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "NLog.LogEventInfo, NLog" })]
        public void Write(LogEventInfoLegacyProxy logEventInfo)
        {
            if (logEventInfo is null)
            {
                return;
            }

            if (logEventInfo.Level.Ordinal < (_minimumLevel ?? (int)TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel))
            {
                return;
            }

            var contextProperties = _getProperties?.Invoke();
            var eventProperties = logEventInfo.HasProperties ? logEventInfo.Properties : null;

            // We render the event to a string immediately as we need to capture the properties
            // This is more expensive from a CPU perspective, but is necessary as the properties
            // won't necessarily be serialized correctly otherwise (e.g. dd_span_id/dd_trace_id)

            var logEvent = new LogEntry(logEventInfo, contextProperties, eventProperties);
            var logFormatter = _formatter ?? TracerManager.Instance.DirectLogSubmission.Formatter;
            var serializedLog = NLogLogFormatter.FormatLogEvent(logFormatter, logEvent);

            (_sink ?? TracerManager.Instance.DirectLogSubmission.Sink).EnqueueLog(new NLogDatadogLogEvent(serializedLog));
        }

        internal void SetGetContextPropertiesFunc(Func<IDictionary<string, object>> func)
        {
            _getProperties = func;
        }
    }
}
