// <copyright file="MappedDiagnosticsLogicalContextLegacyProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for MappedDiagnosticsLogicalContext in NLog &lt;4.3
    /// Async version of Mapped Diagnostics Context - a logical context structure that keeps a dictionary
    /// of strings and provides methods to output them in layouts.  Allows for maintaining state across
    /// asynchronous tasks and call contexts.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct MappedDiagnosticsLogicalContextLegacyProxy
    {
        /// <summary>
        /// Gets the async  local dictionary for the type
        /// </summary>
        public IDictionary LogicalThreadDictionary;
    }
}
