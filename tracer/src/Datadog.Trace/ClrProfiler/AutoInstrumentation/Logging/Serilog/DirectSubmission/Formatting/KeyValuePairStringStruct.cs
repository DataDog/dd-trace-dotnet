// <copyright file="KeyValuePairStringStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission.Formatting
{
    /// <summary>
    /// Duck type for KeyValuePair&lt;object, LogEventPropertyValue&gt;
    /// </summary>
    [DuckCopy]
    [DuckCopy("System.Collections.Generic.KeyValuePair`2[[System.String, System.Private.CoreLib],[Serilog.Events.LogEventPropertyValue, Serilog]]", "System.Private.CoreLib")]
    internal struct KeyValuePairStringStruct
    {
        /// <summary>
        /// Gets the key
        /// </summary>
        public string Key;

        /// <summary>
        /// Gets the value (A LogEventPropertyValue (ScalarValue/StructureValue etc)
        /// </summary>
        public object Value;
    }
}
