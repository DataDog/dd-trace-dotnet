// <copyright file="KeyValuePairObjectStruct.cs" company="Datadog">
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
    internal struct KeyValuePairObjectStruct
    {
        /// <summary>
        /// Gets the key
        /// </summary>
        public object Key;

        /// <summary>
        /// Gets the value (A LogEventPropertyValue (ScalarValue/StructureValue etc)
        /// </summary>
        public object Value;
    }
}
