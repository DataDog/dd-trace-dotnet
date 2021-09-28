// <copyright file="LogEventPropertyDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.Formatting
{
    /// <summary>
    /// Duck type for LogEventProperty
    /// https://github.dev/serilog/serilog/blob/5e93d5045585095ebcb71ef340d6accd61f01670/src/Serilog/Events/LogEventProperty.cs
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DuckCopy]
    public struct LogEventPropertyDuck
    {
        /// <summary>
        /// Gets the name
        /// </summary>
        public string Name;

        /// <summary>
        /// Gets the value
        /// </summary>
        public object Value;
    }
}
