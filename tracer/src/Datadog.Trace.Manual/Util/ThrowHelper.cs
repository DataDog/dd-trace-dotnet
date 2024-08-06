// <copyright file="ThrowHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util
{
    internal class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowArgumentNullException(string paramName) => throw new ArgumentNullException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(string paramName) => throw new ArgumentOutOfRangeException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(string paramName, string message) => throw new ArgumentOutOfRangeException(paramName, message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(string paramName, object actualValue, string message) => throw new ArgumentOutOfRangeException(paramName, actualValue, message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowArgumentException(string message) => throw new ArgumentException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowArgumentException(string message, string paramName) => throw new ArgumentException(message, paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowInvalidOperationException(string message) => throw new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowException(string message) => throw new Exception(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowInvalidCastException(string message) => throw new InvalidCastException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowIndexOutOfRangeException(string message) => throw new IndexOutOfRangeException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowNotSupportedException(string message) => throw new NotSupportedException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowKeyNotFoundException(string message) => throw new KeyNotFoundException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowNullReferenceException(string message) => throw new NullReferenceException(message);
    }
}
