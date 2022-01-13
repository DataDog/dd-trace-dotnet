// <copyright file="ILoggingRuleProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43
{
    /// <summary>
    /// Duck type for LoggingRule for NLog &lt; 4.3
    /// This is left as an interface instead of a [DuckCopy] struct as we need to
    /// set values on the proxy too.
    /// </summary>
    internal interface ILoggingRuleProxy
    {
        /// <summary>
        /// Gets or sets logger name pattern
        /// </summary>
        public string LoggerNamePattern { get; set; }

        /// <summary>
        /// Gets the collection of logging rules
        /// </summary>
        public ITargetListProxy Targets { get; }

        /// <summary>
        /// Gets or sets a value indicating whether to quit processing any further rule when this one matches
        /// </summary>
        public bool Final { get; set; }

        /// <summary>
        /// Gets the loglevels
        /// </summary>
        [DuckField(Name = "logLevels")]
        public bool[] LogLevels { get; }
    }
}
