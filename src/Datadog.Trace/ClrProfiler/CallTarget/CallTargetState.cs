// <copyright file="CallTargetState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// Call target execution state
    /// </summary>
    public readonly struct CallTargetState
    {
        private readonly Scope _previousScope;
        private readonly Scope _scope;
        private readonly object _state;
        private readonly DateTimeOffset? _startTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        public CallTargetState(Scope scope)
        {
            _previousScope = null;
            _scope = scope;
            _state = null;
            _startTime = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        public CallTargetState(Scope scope, object state)
        {
            _previousScope = null;
            _scope = scope;
            _state = state;
            _startTime = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        /// <param name="startTime">The intended start time of the scope, intended for scopes created in the OnMethodEnd handler</param>
        public CallTargetState(Scope scope, object state, DateTimeOffset? startTime)
        {
            _previousScope = null;
            _scope = scope;
            _state = state;
            _startTime = startTime;
        }

        internal CallTargetState(Scope previousScope, CallTargetState state)
        {
            _previousScope = previousScope;
            _scope = state._scope;
            _state = state._state;
            _startTime = state._startTime;
        }

        /// <summary>
        /// Gets the CallTarget BeginMethod scope
        /// </summary>
        public Scope Scope => _scope;

        /// <summary>
        /// Gets the CallTarget BeginMethod state
        /// </summary>
        public object State => _state;

        /// <summary>
        /// Gets the CallTarget state StartTime
        /// </summary>
        public DateTimeOffset? StartTime => _startTime;

        internal Scope PreviousScope => _previousScope;

        /// <summary>
        /// Gets the default call target state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState GetDefault()
        {
            return default;
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(CallTargetState).FullName}({_previousScope}, {_scope}, {_state})";
        }
    }
}
