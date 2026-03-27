// <copyright file="CallTargetAotRegistrationConflictException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Signals that two different generated handlers attempted to register for the same CallTarget NativeAOT binding.
/// </summary>
internal sealed class CallTargetAotRegistrationConflictException : Exception
{
    private CallTargetAotRegistrationConflictException(string detail)
        : base($"Conflicting CallTarget NativeAOT registrations were detected. {detail}")
    {
    }

    /// <summary>
    /// Throws a registration-conflict failure with a deterministic message.
    /// </summary>
    /// <param name="detail">The specific registration conflict detail.</param>
    [DebuggerHidden]
    [DoesNotReturn]
    internal static void ThrowConflict(string detail)
    {
        throw new CallTargetAotRegistrationConflictException(detail);
    }
}
