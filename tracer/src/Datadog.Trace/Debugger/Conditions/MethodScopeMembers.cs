// <copyright file="MethodScopeMembers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#if !NET461
using System.Buffers;
#endif

namespace Datadog.Trace.Debugger.Conditions;

internal class MethodScopeMembers
{
    private int _index = 0;

    public MethodScopeMembers(int numberOfLocals, int numberOfArguments)
    {
        // 3 = return, exception, one to spare
#if NET461
        Members = new ScopeMember[numberOfLocals + numberOfArguments + 3];
#else
        Members = ArrayPool<ScopeMember>.Shared.Rent(numberOfLocals + numberOfArguments + 3);
#endif
    }

    public ScopeMember[] Members { get; private set; }

    public Exception Exception { get; internal set; }

    // we can save Return and InvocationTarget as T if we will change the native side so we will have MethodDebuggerState<T, TReturn> instead MethodDebuggerState
    public ScopeMember Return { get; internal set; }

    public ScopeMember InvocationTarget { get; internal set; }

    internal void AddMember(ScopeMember member)
    {
        Members[_index] = member;
        _index++;
    }

    internal void Clean()
    {
#if NET461
        Members = null;
#else
        ArrayPool<ScopeMember>.Shared.Return(Members);
#endif
    }
}
