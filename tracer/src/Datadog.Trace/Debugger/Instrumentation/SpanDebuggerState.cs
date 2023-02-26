// <copyright file="SpanDebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Call target execution state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SpanDebuggerState
    {
        private readonly Scope _previousScope;
        private readonly Scope _scope;
        private readonly object _state;
        private readonly DateTimeOffset? _startTime;

        private readonly IReadOnlyDictionary<string, string> _previousDistributedSpanContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanDebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        internal SpanDebuggerState(Scope scope)
        {
            _previousScope = null;
            _scope = scope;
            _state = null;
            _startTime = null;
            _previousDistributedSpanContext = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanDebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        internal SpanDebuggerState(Scope scope, object state)
        {
            _previousScope = null;
            _scope = scope;
            _state = state;
            _startTime = null;
            _previousDistributedSpanContext = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanDebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        /// <param name="startTime">The intended start time of the scope, intended for scopes created in the OnMethodEnd handler</param>
        internal SpanDebuggerState(Scope scope, object state, DateTimeOffset? startTime)
        {
            _previousScope = null;
            _scope = scope;
            _state = state;
            _startTime = startTime;
            _previousDistributedSpanContext = null;
        }

        internal SpanDebuggerState(Scope previousScope, IReadOnlyDictionary<string, string> previousDistributedSpanContext, SpanDebuggerState state)
        {
            _previousScope = previousScope;
            _scope = state._scope;
            _state = state._state;
            _startTime = state._startTime;
            _previousDistributedSpanContext = previousDistributedSpanContext;
        }

        /// <summary>
        /// Gets the CallTarget BeginMethod scope
        /// </summary>
        internal Scope Scope => _scope;

        /// <summary>
        /// Gets the CallTarget BeginMethod state
        /// </summary>
        public object State => _state;

        /// <summary>
        /// Gets the CallTarget state StartTime
        /// </summary>
        public DateTimeOffset? StartTime => _startTime;

        internal Scope PreviousScope => _previousScope;

        internal IReadOnlyDictionary<string, string> PreviousDistributedSpanContext => _previousDistributedSpanContext;

        /// <summary>
        /// Gets or sets a value indicating whether we should still be capturing values,
        /// or halt for any reason (e.g an exception was caused by our instrumentation, rate limiter threshold reached).
        /// </summary>
        internal bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets the default call target state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanDebuggerState GetDefault()
        {
            return default;
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(SpanDebuggerState).FullName}({_previousScope}, {_scope}, {_state})";
        }
    }
}
