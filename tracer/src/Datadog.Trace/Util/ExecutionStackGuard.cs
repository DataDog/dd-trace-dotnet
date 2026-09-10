// <copyright file="ExecutionStackGuard.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

/// <summary>
/// Guards operations that walk the current thread's stack. The runtime walks it on that very thread,
/// so doing it on a stack that is already nearly exhausted is enough to cross the guard page, and a
/// <see cref="StackOverflowException"/> cannot be caught - it takes the process down with it.
/// </summary>
internal static class ExecutionStackGuard
{
    public static bool HasSufficientStack()
    {
#if NETCOREAPP
        return RuntimeHelpers.TryEnsureSufficientExecutionStack();
#else
        // .NET Framework only offers the throwing variant. The exception is only paid for on the
        // path where we are about to bail out anyway.
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            return true;
        }
        catch (InsufficientExecutionStackException)
        {
            return false;
        }
#endif
    }
}
