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
            State = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncDebuggerState"/> struct.
        /// </summary>
        /// <param name="state">Customized underlying state that is being used by instrumentation code that is being applied on async methods</param>
        public AsyncDebuggerState(object state)
        {
            State = state;
        }

        /// <summary>
        /// Gets the state
        /// </summary>
        public object State { get; }
    }
}
