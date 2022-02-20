// <copyright file="DebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.SnapshotSerializer;

namespace Datadog.Trace.Debugger
{
    /// <summary>
    /// Live debugger execution state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref struct DebuggerState
    {
        private readonly Scope _scope;
        private readonly DateTimeOffset? _startTime;

        // Determines whether we should still be capturing values, or halt for any reason (e.g an exception was caused by our instrumentation, rate limiter threshold reached).
        internal bool IsActive;

        internal bool HasLocalsOrReturnValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        internal DebuggerState(Scope scope)
            : this(scope, startTime: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerState"/> struct.
        /// </summary>
        /// <param name="state">DebuggerState instance</param>
        internal DebuggerState(DebuggerState state)
            : this(state.Scope, state.StartTime)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="startTime">The intended start time of the scope, intended for scopes created in the OnMethodEnd handler</param>
        internal DebuggerState(Scope scope, DateTimeOffset? startTime)
        {
            _scope = scope;
            _startTime = startTime;
            IsActive = true;
            HasLocalsOrReturnValue = false;
            SnapshotCreator = new DebuggerSnapshotCreator();
        }

        /// <summary>
        /// Gets the LiveDebugger SnapshotCreator
        /// </summary>
        internal DebuggerSnapshotCreator SnapshotCreator { get; }

        /// <summary>
        /// Gets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope => _scope;

        /// <summary>
        /// Gets the LiveDebugger state StartTime
        /// </summary>
        internal DateTimeOffset? StartTime => _startTime;

        /// <summary>
        /// Gets the default live debugger state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerState GetDefault()
        {
            return new DebuggerState();
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
