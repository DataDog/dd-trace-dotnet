// <copyright file="MethodScopeMembers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
#if NET6_0_OR_GREATER
using System.Buffers;
#endif

namespace Datadog.Trace.Debugger.Expressions;

internal class MethodScopeMembers
{
    private int _index;

    internal MethodScopeMembers(int numberOfLocals, int numberOfArguments)
    {
        // 2 for 'return' and 'exception'
#if NET6_0_OR_GREATER
        Members = ArrayPool<ScopeMember>.Shared.Rent(numberOfLocals + numberOfArguments + 2);
#else
        Members = new ScopeMember[numberOfLocals + numberOfArguments + 2];
#endif
        Exception = null;
        Return = default;
        InvocationTarget = default;
    }

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    internal ScopeMember[] Members { get; private set; }

    internal Exception Exception { get; set; }

    // food for thought:
    // we can save Return and InvocationTarget as T if we will change the native side so we will have MethodDebuggerState<T, TReturn> instead MethodDebuggerState.
    internal ScopeMember Return { get; set; }

    internal ScopeMember InvocationTarget { get; set; }

    internal ScopeMember Duration { get; set; }

    internal void AddMember(ScopeMember member)
    {
        Members[_index] = member;
        _index++;
    }

    internal void Dispose()
    {
#if NET6_0_OR_GREATER
        ArrayPool<ScopeMember>.Shared.Return(Members);

#else
        Members = null;
#endif
    }
}
