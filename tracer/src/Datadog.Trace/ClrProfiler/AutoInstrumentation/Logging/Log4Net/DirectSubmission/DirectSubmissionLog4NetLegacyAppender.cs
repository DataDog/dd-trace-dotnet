// <copyright file="DirectSubmissionLog4NetLegacyAppender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    /// <summary>
    /// Duck type for IAppender
    /// </summary>
    internal class DirectSubmissionLog4NetLegacyAppender
    {
        private static DirectSubmissionLog4NetLegacyAppender _instance = null!;

        private readonly IDirectSubmissionLogSink _sink;
        private readonly DirectSubmissionLogLevel _minimumLevel;

        // internal for testing
        internal DirectSubmissionLog4NetLegacyAppender(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel minimumLevel)
        {
            _sink = sink;
            _minimumLevel = minimumLevel;
        }

        internal static DirectSubmissionLog4NetLegacyAppender Instance
        {
            get { return LazyInitializer.EnsureInitialized(ref _instance, CreateStaticInstance); }
        }

        /// <summary>
        /// Gets or sets the name of this appender
        /// </summary>
        [DuckReverseMethod]
        public string Name { get; set; } = "Datadog";

        /// <summary>
        /// Closes the appender and releases resources
        /// </summary>
        [DuckReverseMethod]
        public void Close()
        {
        }

        /// <summary>
        /// Log the logging event in Appender specific way.
        /// </summary>
        /// <param name="logEvent">The logging event</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "log4net.Core.LoggingEvent, log4net " })]
        public void DoAppend(ILoggingEventLegacyDuck? logEvent)
        {
            if (logEvent is null)
            {
                return;
            }

            if (logEvent.Level.ToStandardLevel() < _minimumLevel)
            {
                return;
            }

            var log = new Log4NetDirectSubmissionLogEvent(logEvent, logEvent.TimeStamp.ToUniversalTime());
            TelemetryFactory.Metrics.RecordCountDirectLogLogs(MetricTags.IntegrationName.Log4Net);
            _sink.EnqueueLog(log);
        }

        private static DirectSubmissionLog4NetLegacyAppender CreateStaticInstance()
        {
            return new DirectSubmissionLog4NetLegacyAppender(
                TracerManager.Instance.DirectLogSubmission.Sink,
                TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel);
        }
    }
}
