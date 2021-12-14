// <copyright file="LogEventProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.LogsInjection
{
    /// <summary>
    /// Ducktyping proxy for https://github.com/serilog/serilog/blob/1aabe1d6bde10382233fb2a50e0e2c6e0c9b8287/src/Serilog/Events/LogEvent.cs
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DuckCopy]
    public struct LogEventProxy
    {
        /// <summary>
        /// Gets the log Properties collection
        /// </summary>
        [DuckField(Name = "_properties")]
        public IDictionary Properties;
    }
}
