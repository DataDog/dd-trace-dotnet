// <copyright file="ILogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission
{
    /// <summary>
    /// Duck type for LogEvent
    /// </summary>
    internal interface ILogEvent : IDuckType
    {
        /// <summary>
        /// Gets the time at which the event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the level of the event.
        /// </summary>
        public LogEventLevelDuck Level { get; }

        /// <summary>
        /// Gets the message template describing the event.
        /// </summary>
        public MessageTemplateProxy MessageTemplate { get; }

        /// <summary>
        /// Gets properties associated with the event, including those presented in <see cref="MessageTemplate"/>.
        /// </summary>
        public IEnumerable Properties { get; }

        /// <summary>
        /// Gets an exception associated with the event, or null.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Render the message template given the properties associated
        /// with the event, and return the result.
        /// </summary>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <returns>The rendered message</returns>
        public string RenderMessage(IFormatProvider formatProvider = null);
    }
}
