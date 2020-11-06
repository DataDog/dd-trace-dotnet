using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// Call target execution state
    /// </summary>
    public readonly struct CallTargetState
    {
        private readonly object _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="CallTargetState"/> struct.
        /// </summary>
        /// <param name="state">Object state instance</param>
        public CallTargetState(object state)
        {
            _state = state;
        }

        /// <summary>
        /// Gets the CallTarget BeginMethod state
        /// </summary>
        public object State => _state;

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
            return $"{typeof(CallTargetState).FullName}({_state})";
        }
    }
}
