// <copyright file="NLogDirectSubmissionLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting
{
    internal class NLogDirectSubmissionLogEvent : DirectSubmissionLogEvent
    {
        private readonly string _serializedEvent;

        public NLogDirectSubmissionLogEvent(string serializedEvent)
        {
            _serializedEvent = serializedEvent;
        }

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            sb.Append(_serializedEvent);
        }
    }
}
