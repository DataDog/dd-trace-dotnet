// <copyright file="LogEventInfoProxyBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for LogEventInfo  for NLog &lt; 4.5
    /// Using virtual members, as will need to be boxed, so no advantage from using a struct
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class LogEventInfoProxyBase
    {
        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public virtual DateTime TimeStamp { get; }

        /// <summary>
        /// Gets the log level
        /// </summary>
        public virtual LogLevelProxy Level { get; }

        /// <summary>
        /// Gets the stack trace
        /// </summary>
        public virtual StackTrace StackTrace { get; }

        /// <summary>
        /// Gets the exception
        /// </summary>
        public virtual Exception Exception { get; }

        /// <summary>
        /// Gets the logger name.
        /// </summary>
        public virtual string LoggerName { get; }

        /// <summary>
        /// Gets the formatted message.
        /// </summary>
        public virtual string FormattedMessage { get; }

        /// <summary>
        /// Gets the format provider.
        /// </summary>
        public virtual IFormatProvider FormatProvider { get; }

        /// <summary>
        /// Gets the log message including any parameter placeholders.
        /// </summary>
        public virtual string Message { get; }
    }
}
