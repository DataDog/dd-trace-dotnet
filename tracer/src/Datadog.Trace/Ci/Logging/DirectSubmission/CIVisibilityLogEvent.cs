// <copyright file="CIVisibilityLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Text;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.Ci.Logging.DirectSubmission
{
    internal class CIVisibilityLogEvent : DirectSubmissionLogEvent
    {
        private readonly string _source;
        private readonly string? _logLevel;
        private readonly string _message;
        private readonly ISpan? _span;

        public CIVisibilityLogEvent(string source, string? logLevel, string message, ISpan? span)
        {
            _source = source;
            _logLevel = logLevel;
            _message = message;
            _span = span;
        }

        public override void Format(StringBuilder sb, LogFormatter formatter)
        {
            formatter.FormatCIVisibilityLog(sb, _source, _logLevel, _message, _span);
        }
    }
}
