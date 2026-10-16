// <copyright file="RuntimeTypeCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// A check of <c>IntegrationMapper</c> that depends on the instantiation of a generic target. The registration runs it
/// with <c>typeof</c> of both types and records <see cref="Message"/> as a failure when it doesn't hold.
/// </summary>
internal sealed class RuntimeTypeCheck
{
    public RuntimeTypeCheck(bool sameType, TypeSig left, TypeSig right, string message)
    {
        SameType = sameType;
        Left = left;
        Right = right;
        Message = message;
    }

    /// <summary>Gets a value indicating whether the types must be the same; otherwise <see cref="Left"/> must be assignable from <see cref="Right"/>.</summary>
    public bool SameType { get; }

    public TypeSig Left { get; }

    public TypeSig Right { get; }

    public string Message { get; }
}
#endif
