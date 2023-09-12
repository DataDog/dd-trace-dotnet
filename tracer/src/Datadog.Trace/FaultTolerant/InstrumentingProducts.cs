// <copyright file="InstrumentingProducts.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.FaultTolerant
{
    /// <summary>
    /// Describes the instrumenting products used in Fault-Tolerant instrumentation.
    /// Matches a native `enum class` of the same structure.
    /// </summary>
    [Flags]
    internal enum InstrumentingProducts
    {
        /// <summary>
        /// The tracer instrumentation.
        /// </summary>
        Tracer = 1,

        /// <summary>
        /// The Dynamic Instrumentation (aka Live Debugger / Debugger) instrumentation.
        /// </summary>
        DynamicInstrumentation = 2,

        /// <summary>
        /// The ASM instrumentation.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        ASM = 4
    }
}
