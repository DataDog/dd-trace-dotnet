// <copyright file="ILoggingConfigurationPre43Proxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43
{
    /// <summary>
    /// Duck type for LoggingConfiguration for NLog &lt; 4.3
    /// </summary>
    internal interface ILoggingConfigurationPre43Proxy
    {
        /// <summary>
        /// Gets a collection of named targets specified in the configuration.
        /// </summary>
        public IEnumerable ConfiguredNamedTargets { get; }

        /// <summary>
        /// Gets the collection of logging rules
        /// </summary>
        public ILoggingRulesListProxy LoggingRules { get; }

        /// <summary>
        /// Registers the specified target object under a given name.
        /// </summary>
        /// <param name="name">Name of the target.</param>
        /// <param name="target">The target object.</param>
        [Duck(ParameterTypeNames = new[] { ClrNames.String, "NLog.Targets.Target, NLog" })]
        public void AddTarget(string name, object target);
    }
}
