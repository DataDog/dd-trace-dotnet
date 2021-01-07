using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// Call target execution state
    /// </summary>
    public readonly struct CallTargetState
    {
        private readonly Scope _oldScope;
        private readonly Scope _scope;
        private readonly object _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        public CallTargetState(Scope scope)
        {
            _oldScope = null;
            _scope = scope;
            _state = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="scope">Scope instance</param>
        /// <param name="state">Object state instance</param>
        public CallTargetState(Scope scope, object state)
        {
            _oldScope = null;
            _scope = scope;
            _state = state;
        }

        private CallTargetState(Scope oldScope, Scope scope, object state)
        {
            _oldScope = oldScope;
            _scope = scope;
            _state = state;
        }

        /// <summary>
        /// Gets the CallTarget BeginMethod scope
        /// </summary>
        public Scope Scope => _scope;

        /// <summary>
        /// Gets the CallTarget BeginMethod state
        /// </summary>
        public object State => _state;

        internal Scope OldScope => _oldScope;

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
            return $"{typeof(CallTargetState).FullName}({_oldScope}, {_scope}, {_state})";
        }

        internal static CallTargetState WithPreviousScope(Scope oldScope, CallTargetState state)
        {
            return new CallTargetState(oldScope, state._scope, state._state);
        }
    }
}
