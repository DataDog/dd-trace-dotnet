// <copyright file="MappedDiagnosticsProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies
{
    /// <summary>
    /// Duck type for MappedDiagnosticsContext and MappedDiagnosticsLogicalContext in NLog 4.3+
    /// Async version of Mapped Diagnostics Context - a logical context structure that keeps a dictionary
    /// of strings and provides methods to output them in layouts.  Allows for maintaining state across
    /// asynchronous tasks and call contexts.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1400
#pragma warning disable SA1302
    public interface MappedDiagnosticsProxy
#pragma warning restore SA1302
#pragma warning restore SA1400
    {
        /// <summary>
        /// Gets the item names
        /// </summary>
        /// <returns>A collection of the names of all items in current logical context.</returns>
        public ICollection<string> GetNames();

        /// <summary>
        /// Gets the current logical context named item, as <see cref="object"/>
        /// </summary>
        /// <param name="item">Item name</param>
        /// <returns>The value of <paramref name="item"/>, if defined; otherwise <c>null</c>.</returns>
        public object GetObject(string item);
    }
}
