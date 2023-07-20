// <copyright file="Log4NetDirectSubmissionLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Text;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    internal class Log4NetDirectSubmissionLogEvent : DirectSubmissionLogEvent
    {
        private readonly ILoggingEventDuckBase _logEvent;
        private readonly DateTime _timestamp;

        public Log4NetDirectSubmissionLogEvent(ILoggingEventDuckBase logEvent, DateTime timestamp)
        {
            _logEvent = logEvent;
            _timestamp = timestamp;
        }

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            Log4NetLogFormatter.FormatLogEvent(formatter, sb, _logEvent, _timestamp);
        }
    }
}
