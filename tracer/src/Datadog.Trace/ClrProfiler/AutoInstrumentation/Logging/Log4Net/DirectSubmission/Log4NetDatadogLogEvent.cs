// <copyright file="Log4NetDatadogLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    internal class Log4NetDatadogLogEvent : DatadogLogEvent
    {
        private readonly LoggingEventDuck _logEvent;

        public Log4NetDatadogLogEvent(LoggingEventDuck logEvent)
        {
            _logEvent = logEvent;
        }

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            Log4NetLogFormatter.FormatLogEvent(formatter, sb, _logEvent);
        }
    }
}
