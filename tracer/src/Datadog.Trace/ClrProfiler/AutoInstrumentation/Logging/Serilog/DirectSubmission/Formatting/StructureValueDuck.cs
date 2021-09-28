// <copyright file="StructureValueDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.Formatting
{
    /// <summary>
    /// Duck type for ScalarValue
    /// https://github.dev/serilog/serilog/blob/5e93d5045585095ebcb71ef340d6accd61f01670/src/Serilog/Events/ScalarValue.cs
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DuckCopy]
    public struct StructureValueDuck
    {
        /// <summary>
        /// Gets the properties associated with the structure
        /// These are convertible to <see cref="LogEventPropertyDuck"/>
        /// </summary>
        public IEnumerable Properties;

        /// <summary>
        /// Gets the type of the structure
        /// </summary>
        public string TypeTag;
    }
}
