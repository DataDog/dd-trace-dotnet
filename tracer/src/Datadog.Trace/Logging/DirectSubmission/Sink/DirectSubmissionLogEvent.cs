// <copyright file="DirectSubmissionLogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Text;
using Datadog.Trace.Logging.DirectSubmission.Formatting;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal abstract class DirectSubmissionLogEvent
    {
        /// <summary>
        /// Formats the event to the provided <see cref="StringBuilder"/>
        /// </summary>
        /// <param name="sb">The builder to format the log into</param>
        /// <param name="formatter">A formatter for log events</param>
        public abstract void Format(StringBuilder sb, LogFormatter formatter);
    }
}
