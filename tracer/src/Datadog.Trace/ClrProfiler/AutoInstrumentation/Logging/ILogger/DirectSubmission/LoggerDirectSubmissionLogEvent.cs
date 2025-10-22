// <copyright file="LoggerDirectSubmissionLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Text;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.OpenTelemetry.Logs;
#endif

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    internal class LoggerDirectSubmissionLogEvent : DirectSubmissionLogEvent
    {
        private readonly string? _serializedEvent;

        public LoggerDirectSubmissionLogEvent(string? serializedEvent)
        {
            _serializedEvent = serializedEvent;
        }

#if NETCOREAPP3_1_OR_GREATER
        public LogPoint? OtlpLog { get; set; }
#endif

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            sb.Append(_serializedEvent);
        }
    }
}
