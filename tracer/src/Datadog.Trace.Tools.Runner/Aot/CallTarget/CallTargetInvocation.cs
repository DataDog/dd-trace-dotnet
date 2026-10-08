// <copyright file="CallTargetInvocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// One <c>CallTargetInvoker</c> instantiation found in a rewritten method. The types are expressed in the generic
/// context of that method (<c>!n</c> for the declaring type, <c>!!n</c> for the method).
/// </summary>
internal sealed class CallTargetInvocation
{
    public CallTargetInvocation(CallTargetInvocationKind kind, TypeSig integration, TypeSig target, IReadOnlyList<TypeSig> arguments, TypeSig? returnType, TypeSig? declaredReturnType)
    {
        Kind = kind;
        Integration = integration;
        Target = target;
        Arguments = arguments;
        ReturnType = returnType;
        DeclaredReturnType = declaredReturnType;
    }

    public CallTargetInvocationKind Kind { get; }

    public TypeSig Integration { get; }

    public TypeSig Target { get; }

    /// <summary>Gets the argument types of <see cref="CallTargetInvocationKind.Begin"/>.</summary>
    public IReadOnlyList<TypeSig> Arguments { get; }

    /// <summary>Gets the return type of the end methods: the declared one, or the unwrapped one for runtime-async.</summary>
    public TypeSig? ReturnType { get; }

    /// <summary>Gets the Task or ValueTask a runtime-async method declares.</summary>
    public TypeSig? DeclaredReturnType { get; }

    public CallTargetInvocation Remap(Func<TypeSig, TypeSig> remap)
    {
        var arguments = new TypeSig[Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = remap(Arguments[i]);
        }

        return new CallTargetInvocation(
            Kind,
            remap(Integration),
            remap(Target),
            arguments,
            ReturnType is null ? null : remap(ReturnType),
            DeclaredReturnType is null ? null : remap(DeclaredReturnType));
    }

    public override string ToString()
    {
        var types = new List<string> { Integration.FullName, Target.FullName };
        foreach (var argument in Arguments)
        {
            types.Add(argument.FullName);
        }

        if (ReturnType is not null)
        {
            types.Add(ReturnType.FullName);
        }

        if (DeclaredReturnType is not null)
        {
            types.Add(DeclaredReturnType.FullName);
        }

        return $"{Kind}<{string.Join(", ", types)}>";
    }
}
#endif
