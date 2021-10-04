// <copyright file="DirectSubmissionLog4NetAppender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    /// <summary>
    /// Duck type for IAppender
    /// </summary>
    public class DirectSubmissionLog4NetAppender
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DirectSubmissionLog4NetAppender>();
        private static DirectSubmissionLog4NetAppender _instance;

        private readonly IDatadogSink _sink;
        private readonly DirectSubmissionLogLevel _minimumLevel;

        internal DirectSubmissionLog4NetAppender(IDatadogSink sink, DirectSubmissionLogLevel minimumLevel)
        {
            _sink = sink;
            _minimumLevel = minimumLevel;
        }

        internal static DirectSubmissionLog4NetAppender Instance
        {
            get { return LazyInitializer.EnsureInitialized(ref _instance, CreateStaticInstance); }
        }

        /// <summary>
        /// Gets or sets the name of this appender
        /// </summary>
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
        [DuckReverseMethod("log4net.Core.LoggingEvent")]
        public void DoAppend(LoggingEventDuck logEvent)
        {
            if (logEvent is null)
            {
                return;
            }

            if (logEvent.Level.ToStandardLevel() < _minimumLevel)
            {
                return;
            }

            _sink.EnqueueLog(new Log4NetDatadogLogEvent(logEvent));
        }

        private static DirectSubmissionLog4NetAppender CreateStaticInstance()
        {
            return new DirectSubmissionLog4NetAppender(
                DirectLogSubmission.Instance.Sink,
                DirectLogSubmission.Instance.Settings.MinimumLevel);
        }
    }
}
