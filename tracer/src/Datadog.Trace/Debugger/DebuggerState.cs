// <copyright file="DebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Debugger
{
    /// <summary>
    /// Live debugger execution state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct DebuggerState
    {
        private readonly Scope _scope;
        private readonly DateTimeOffset? _startTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        internal DebuggerState(Scope scope)
        {
            _scope = scope;
            _startTime = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        internal DebuggerState(Scope scope, object state)
        {
            _scope = scope;
            _startTime = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        /// <param name="startTime">The intended start time of the scope, intended for scopes created in the OnMethodEnd handler</param>
        internal DebuggerState(Scope scope, object state, DateTimeOffset? startTime)
        {
            _scope = scope;
            _startTime = startTime;
        }

        internal DebuggerState(DebuggerState state)
        {
            _scope = state._scope;
            _startTime = state._startTime;
        }

        /// <summary>
        /// Gets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope => _scope;

        /// <summary>
        /// Gets the LiveDebugger state StartTime
        /// </summary>
        public DateTimeOffset? StartTime => _startTime;

        /// <summary>
        /// Gets the default live debugger state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerState GetDefault()
        {
            return default;
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(DebuggerState).FullName}({_scope})";
        }
    }
}
