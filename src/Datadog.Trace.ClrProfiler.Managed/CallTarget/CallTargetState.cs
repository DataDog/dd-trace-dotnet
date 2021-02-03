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

        private CallTargetState(Scope previousScope, Scope scope, object state)
        {
            _previousScope = previousScope;
            _scope = scope;
            _state = state;
            _startTime = null;
        }

        /// <summary>
        /// Gets the CallTarget BeginMethod scope
        /// </summary>
        public Scope Scope => _scope;

        /// <summary>
        /// Gets the CallTarget BeginMethod state
        /// </summary>
        public object State => _state;

        internal Scope PreviousScope => _previousScope;

        internal DateTimeOffset? StartTime => _startTime;

        /// <summary>
        /// Gets the default call target state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState GetDefault()
        {
            return new CallTargetState(null);
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(CallTargetState).FullName}({_previousScope}, {_scope}, {_state})";
        }

        internal static CallTargetState WithPreviousScope(Scope previousScope, CallTargetState state)
        {
            return new CallTargetState(previousScope, state._scope, state._state);
        }
    }
}
