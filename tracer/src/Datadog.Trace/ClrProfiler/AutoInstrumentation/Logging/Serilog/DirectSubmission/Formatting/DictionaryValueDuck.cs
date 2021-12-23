// <copyright file="DictionaryValueDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission.Formatting
{
    /// <summary>
    /// Duck type for ScalarValue
    /// https://github.dev/serilog/serilog/blob/5e93d5045585095ebcb71ef340d6accd61f01670/src/Serilog/Events/ScalarValue.cs
    /// </summary>
    [DuckCopy]
    internal struct DictionaryValueDuck
    {
        /// <summary>
        /// Gets the properties associated with the structure
        /// </summary>
        public IEnumerable Elements;
    }
}
