// <copyright file="CallTargetAotRegistryContractValidationException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Signals that a generated CallTarget NativeAOT registry contract does not match the current Datadog.Trace runtime.
/// </summary>
internal sealed class CallTargetAotRegistryContractValidationException : Exception
{
    private CallTargetAotRegistryContractValidationException(string detail)
        : base($"CallTarget NativeAOT registry contract validation failed. {detail}")
    {
    }

    /// <summary>
    /// Throws a registry-contract validation failure with a deterministic message.
    /// </summary>
    /// <param name="detail">The specific validation detail.</param>
    [DebuggerHidden]
    [DoesNotReturn]
    internal static void ThrowValidation(string detail)
    {
        throw new CallTargetAotRegistryContractValidationException(detail);
    }
}
