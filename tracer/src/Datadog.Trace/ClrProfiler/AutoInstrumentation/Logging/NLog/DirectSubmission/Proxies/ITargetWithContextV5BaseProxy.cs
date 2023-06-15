// <copyright file="ITargetWithContextV5BaseProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for TargetWithContext
    /// Represents target that supports context capture using ScopeContext
    /// </summary>
    internal interface ITargetWithContextV5BaseProxy
    {
        /// <summary>
        /// Gets or sets a value indicating whether gets or sets the option to include all properties from the log events
        /// </summary>
        public bool IncludeEventProperties { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include the contents of the ScopeContext properties-dictionary.
        /// </summary>
        /// <docgen category='Layout Options' order='10' />
        public bool IncludeScopeProperties { get; set; }

        /// <summary>
        /// Creates combined dictionary of all configured properties for logEvent
        /// </summary>
        /// <param name="logEvent">The event to record</param>
        /// <returns>Dictionary with all collected properties for logEvent</returns>
        public IDictionary<string, object> GetAllProperties(ILogEventInfoProxy logEvent);

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        [Duck(ExplicitInterfaceTypeName = "NLog.Internal.ISupportsInitialize", ParameterTypeNames = new[] { "NLog.Config.LoggingConfiguration" })]
        public void Initialize(object configuration);
    }
}
