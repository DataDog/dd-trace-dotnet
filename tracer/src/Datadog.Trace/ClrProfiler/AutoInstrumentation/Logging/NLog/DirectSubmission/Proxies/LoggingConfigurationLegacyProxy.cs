// <copyright file="LoggingConfigurationLegacyProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for LoggingConfiguration for NLog &gt; 4.3-4.5
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggingConfigurationLegacyProxy
    {
        /// <summary>
        /// Gets a collection of named targets specified in the configuration.
        /// </summary>
        public virtual IEnumerable ConfiguredNamedTargets { get; }

        /// <summary>
        /// Registers the specified target object under a given name.
        /// </summary>
        /// <param name="name">Name of the target.</param>
        /// <param name="target">The target object.</param>
        [Duck(ParameterTypeNames = new[] { ClrNames.String, "NLog.Targets.Target, NLog" })]
        public virtual void AddTarget(string name, object target)
        {
        }

        /// <summary>
        /// Add a rule for all loglevels.
        /// </summary>
        /// <param name="target">Target to be written to when the rule matches.</param>
        /// <param name="loggerNamePattern">Logger name pattern. It may include the '*' wildcard at the beginning, at the end or at both ends.</param>
        [Duck(ParameterTypeNames = new[] { "NLog.Targets.Target, NLog", ClrNames.String })]
        public virtual void AddRuleForAllLevels(object target, string loggerNamePattern)
        {
        }
    }
}
