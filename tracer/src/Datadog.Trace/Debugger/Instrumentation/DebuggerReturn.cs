// <copyright file="DebuggerReturn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Live debugger return value
    /// </summary>
    /// <typeparam name="T">Type of the return value</typeparam>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly ref struct DebuggerReturn<T>
    {
        private readonly T _returnValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerReturn{T}"/> struct.
        /// </summary>
        /// <param name="returnValue">Return value</param>
        public DebuggerReturn(T returnValue)
        {
            _returnValue = returnValue;
        }

        /// <summary>
        /// Gets the default live debugger return value (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default live debugger return value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<T> GetDefault()
        {
            return default;
        }

        /// <summary>
        /// Gets the return value
        /// </summary>
        /// <returns>Return value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetReturnValue() => _returnValue;

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(DebuggerReturn<T>).FullName}({_returnValue})";
        }
    }

    /// <summary>
    /// Live debugger return value
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly ref struct DebuggerReturn
    {
        /// <summary>
        /// Gets the default live debugger return value (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default live debugger return value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn GetDefault()
        {
            return default;
        }
    }
}
