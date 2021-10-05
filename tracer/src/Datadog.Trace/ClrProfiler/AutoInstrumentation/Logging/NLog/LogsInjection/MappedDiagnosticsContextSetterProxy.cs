// <copyright file="MappedDiagnosticsContextSetterProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    /// <summary>
    /// Duck type for MappedDiagnosticsContext in NLog 1.0+
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    public class MappedDiagnosticsContextSetterProxy
    {
        /// <summary>
        /// Sets the current thread MDC item to the specified value.
        /// </summary>
        /// <param name="item">Item name.</param>
        /// <param name="value">Item value.</param>
        public virtual void Set(string item, string value)
        {
        }

        /// <summary>
        /// Removes the specified item from current thread MDC.
        /// </summary>
        /// <param name="item">Item name.</param>
        public virtual void Remove(string item)
        {
        }
    }
}
