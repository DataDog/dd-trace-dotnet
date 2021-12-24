// <copyright file="MappedDiagnosticsContextLegacyProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for MappedDiagnosticsContextProxy for NLog &lt; 4.3
    /// Mapped Diagnostics Context - a thread-local structure that keeps a dictionary
    /// of strings and provides methods to output them in layouts.
    /// </summary>
    internal struct MappedDiagnosticsContextLegacyProxy
    {
        /// <summary>
        /// Gets the thread local dictionary for the type
        /// Using an IDictionary instead of typed as in 4.0.x  this is a (string, string),
        /// and in later versions it's a (string, object)
        /// </summary>
        public IDictionary ThreadDictionary;
    }
}
