// <copyright file="ProbeExpressionsCacheEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed class ProbeExpressionsCacheEntry
{
    private readonly object _compileLock;
    private int _compiledInitialized; // 0 = not compiled, 1 = compiled
    private CompiledProbeExpressions _compiled;

    public ProbeExpressionsCacheEntry(Type?[] memberRuntimeTypes)
    {
        MemberRuntimeTypes = memberRuntimeTypes;
        _compileLock = new object();
        _compiledInitialized = 0;
        _compiled = default;
    }

    public Type?[] MemberRuntimeTypes { get; }

    public bool TryGetCompiled(out CompiledProbeExpressions compiled)
    {
        if (Volatile.Read(ref _compiledInitialized) == 1)
        {
            compiled = _compiled;
            return true;
        }

        compiled = default;
        return false;
    }

    public CompiledProbeExpressions GetOrCompile(ProbeExpressionEvaluator evaluator, MethodScopeMembers scopeMembers)
    {
        if (Volatile.Read(ref _compiledInitialized) == 1)
        {
            return _compiled;
        }

        lock (_compileLock)
        {
            if (_compiledInitialized == 0)
            {
                _compiled = evaluator.CompileAll(scopeMembers);
                Volatile.Write(ref _compiledInitialized, 1);
            }
        }

        return _compiled;
    }

    public bool Matches(ScopeMember[] members, int memberCount)
    {
        if (MemberRuntimeTypes.Length != memberCount)
        {
            return false;
        }

        for (int i = 0; i < memberCount; i++)
        {
            var runtimeType = members[i].Value?.GetType() ?? members[i].Type;
            if (MemberRuntimeTypes[i] != runtimeType)
            {
                return false;
            }
        }

        return true;
    }

    public bool Matches(Type?[] memberRuntimeTypes)
    {
        if (MemberRuntimeTypes.Length != memberRuntimeTypes.Length)
        {
            return false;
        }

        for (int i = 0; i < memberRuntimeTypes.Length; i++)
        {
            if (MemberRuntimeTypes[i] != memberRuntimeTypes[i])
            {
                return false;
            }
        }

        return true;
    }
}
