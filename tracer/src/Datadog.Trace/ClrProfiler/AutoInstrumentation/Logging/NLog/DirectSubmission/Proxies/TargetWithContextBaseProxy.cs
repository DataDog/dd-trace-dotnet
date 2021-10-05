// <copyright file="TargetWithContextBaseProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for TargetWithContext
    /// Represents target that supports context capture using MDLC, MDC, NDLC and NDC
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class TargetWithContextBaseProxy
    {
        /// <summary>
        /// Gets or sets a value indicating whether gets or sets the option to include all properties from the log events
        /// </summary>
        public virtual bool IncludeEventProperties { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include contents of the MappedDiagnosticsContext dictionary.
        /// </summary>
        /// <docgen category='Payload Options' order='10' />
        public virtual bool IncludeMdc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include contents of the NestedDiagnosticsContext stack.
        /// </summary>
        /// <docgen category='Payload Options' order='10' />
        public virtual bool IncludeNdc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include contents of the MappedDiagnosticsLogicalContext dictionary.
        /// </summary>
        /// <docgen category='Payload Options' order='10' />
        public virtual bool IncludeMdlc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include contents of the NestedDiagnosticsLogicalContext stack.
        /// </summary>
        /// <docgen category='Payload Options' order='10' />
        public virtual bool IncludeNdlc { get; set; }

        /// <summary>
        /// Creates combined dictionary of all configured properties for logEvent
        /// </summary>
        /// <param name="logEvent">The event to record</param>
        /// <returns>Dictionary with all collected properties for logEvent</returns>
        public virtual IDictionary<string, object> GetAllProperties(LogEventInfoProxy logEvent) => null;

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        [Duck(ExplicitInterfaceTypeName = "NLog.Internal.ISupportsInitialize", ParameterTypeNames = new[] { "NLog.Config.LoggingConfiguration" })]
        public virtual void Initialize(object configuration)
        {
        }
    }
}
