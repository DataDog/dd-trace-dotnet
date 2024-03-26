// <copyright file="AsyncDebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// A state that is used in async continuation
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AsyncDebuggerState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncDebuggerState"/> struct.
        /// </summary>
        public AsyncDebuggerState()
        {
            SpanState = null;
            LogStates = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncDebuggerState"/> struct.
        /// </summary>
        /// <param name="spanState">The span state</param>
        public AsyncDebuggerState(SpanDebuggerState spanState)
        {
            SpanState = spanState;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncDebuggerState"/> struct.
        /// </summary>
        /// <param name="logStates">The log state</param>
        public AsyncDebuggerState(AsyncMethodDebuggerState[] logStates)
        {
            LogStates = logStates;
        }

        /// <summary>
        /// Gets or sets the span state
        /// </summary>
        public SpanDebuggerState? SpanState { get; set; }

        /// <summary>
        /// Gets or sets the log states for multi-probe
        /// </summary>
        public AsyncMethodDebuggerState[] LogStates { get; set; }
    }
}
