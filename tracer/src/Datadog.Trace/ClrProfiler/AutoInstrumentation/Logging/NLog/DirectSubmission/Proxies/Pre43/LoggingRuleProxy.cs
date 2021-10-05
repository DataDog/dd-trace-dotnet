// <copyright file="LoggingRuleProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43
{
    /// <summary>
    /// Duck type for LoggingRule for NLog &lt; 4.3
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggingRuleProxy
    {
        /// <summary>
        /// Gets or sets logger name pattern
        /// </summary>
        public virtual string LoggerNamePattern { get; set; }

        /// <summary>
        /// Gets the collection of logging rules
        /// </summary>
        public virtual TargetListProxy Targets { get; }

        /// <summary>
        /// Gets or sets a value indicating whether to quit processing any further rule when this one matches
        /// </summary>
        public virtual bool Final { get; set; }

        /// <summary>
        /// Gets the loglevels
        /// </summary>
        [DuckField(Name = "logLevels")]
        public virtual bool[] LogLevels { get; }
    }
}
