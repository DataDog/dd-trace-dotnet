// <copyright file="LoggerDirectSubmissionLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Text;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    internal class LoggerDirectSubmissionLogEvent : DirectSubmissionLogEvent
    {
        private readonly string? _serializedEvent;

        public LoggerDirectSubmissionLogEvent(string? serializedEvent)
        {
            _serializedEvent = serializedEvent;
        }

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            sb.Append(_serializedEvent);
        }
    }
}
