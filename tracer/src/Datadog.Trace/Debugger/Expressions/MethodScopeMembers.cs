// <copyright file="MethodScopeMembers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Debugger.Expressions;

internal record struct MethodScopeMembersParameters(int NumberOfLocals, int NumberOfArguments);
internal class MethodScopeMembers : IPoolable<MethodScopeMembersParameters>
{
    private int _index;

    internal ScopeMember[] Members { get; private set; }

    internal Exception Exception { get; set; }

    // food for thought:
    // we can save Return and InvocationTarget as T if we will change the native side, so we will have MethodDebuggerState<T, TReturn> instead MethodDebuggerState.
    internal ScopeMember Return { get; set; }

    internal ScopeMember InvocationTarget { get; set; }

    internal ScopeMember Duration { get; set; }

    internal void AddMember(ScopeMember member)
    {
        if (_index >= Members.Length)
        {
            Members = Members.EnlargeBuffer(_index);
        }

        Members[_index] = member;
        _index++;
    }

    internal void Dispose()
    {
        if (Members != null)
        {
            ArrayPool<ScopeMember>.Shared.Return(Members);
        }
    }

    public void Set(MethodScopeMembersParameters parameters)
    {
        var initialSize = parameters.NumberOfLocals + parameters.NumberOfArguments;
        if (initialSize == 0)
        {
            initialSize = 1;
        }

        Members = ArrayPool<ScopeMember>.Shared.Rent(initialSize * 2);
        Array.Clear(Members, 0, Members.Length);
        Exception = null;
        Duration = default;
        Return = default;
        InvocationTarget = default;
        _index = 0;
    }

    public void Reset()
    {
        Dispose();
    }
}
