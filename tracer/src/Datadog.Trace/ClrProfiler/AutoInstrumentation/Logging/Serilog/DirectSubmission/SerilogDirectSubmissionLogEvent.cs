// <copyright file="SerilogDirectSubmissionLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission
{
    internal class SerilogDirectSubmissionLogEvent : DirectSubmissionLogEvent
    {
        private readonly ILogEvent _logEvent;

        public SerilogDirectSubmissionLogEvent(ILogEvent logEvent)
        {
            _logEvent = logEvent;
        }

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            SerilogLogFormatter.FormatLogEvent(formatter, sb, _logEvent);
        }
    }
}
