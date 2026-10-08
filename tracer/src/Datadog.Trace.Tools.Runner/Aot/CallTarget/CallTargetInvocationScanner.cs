// <copyright file="CallTargetInvocationScanner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Finds the <c>CallTargetInvoker</c> instantiations of a rewritten method body (B1).
/// </summary>
internal static class CallTargetInvocationScanner
{
    internal const string CallTargetInvokerTypeName = "Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvoker";

    public static List<CallTargetInvocation> Scan(CilBody body)
    {
        var invocations = new List<CallTargetInvocation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instruction in body.Instructions)
        {
            if (instruction.OpCode.Code != Code.Call
             || instruction.Operand is not MethodSpec { Method: { } method, GenericInstMethodSig: { } instantiation }
             || method.DeclaringType?.FullName != CallTargetInvokerTypeName)
            {
                continue;
            }

            var invocation = Classify(method.Name, method.MethodSig, instantiation.GenericArguments);
            if (invocation is not null && seen.Add(invocation.ToString()))
            {
                invocations.Add(invocation);
            }
        }

        return invocations;
    }

    private static CallTargetInvocation? Classify(string name, MethodSig signature, IList<TypeSig> types)
    {
        var none = Array.Empty<TypeSig>();
        switch (name)
        {
            case "BeginMethod" when types.Count == 2 && signature.Params.Count == 2 && signature.Params[1] is SZArraySig { Next.ElementType: ElementType.Object }:
                return new CallTargetInvocation(CallTargetInvocationKind.BeginSlow, types[0], types[1], none, null, null);
            case "BeginMethod" when types.Count >= 2:
                return new CallTargetInvocation(CallTargetInvocationKind.Begin, types[0], types[1], types.Skip(2).ToArray(), null, null);
            case "EndMethod" when types.Count == 2:
                return new CallTargetInvocation(CallTargetInvocationKind.EndVoid, types[0], types[1], none, null, null);
            case "EndMethod" when types.Count == 3:
                return new CallTargetInvocation(CallTargetInvocationKind.EndReturn, types[0], types[1], none, types[2], null);
            case "EndMethodRuntimeAsync" when types.Count == 3:
                return new CallTargetInvocation(CallTargetInvocationKind.EndRuntimeAsyncVoid, types[0], types[1], none, null, types[2]);
            case "EndMethodRuntimeAsync" when types.Count == 4:
                return new CallTargetInvocation(CallTargetInvocationKind.EndRuntimeAsyncReturn, types[0], types[1], none, types[2], types[3]);
            default:
                // LogException, GetDefaultValue and CreateRefStruct don't bind integration methods.
                return null;
        }
    }
}
#endif
