using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Trace
{
    /// <summary>
    /// Internal helper class to throw exception (to allow inlining on caller methods)
    /// </summary>
    internal static class ThrowHelper
    {
        [DebuggerHidden]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [DebuggerHidden]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void InvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }
    }
}
