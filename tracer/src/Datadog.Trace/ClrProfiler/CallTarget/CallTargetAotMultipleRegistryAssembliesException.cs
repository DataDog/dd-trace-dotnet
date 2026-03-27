// <copyright file="CallTargetAotMultipleRegistryAssembliesException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Signals that CallTarget NativeAOT detected multiple generated registry assemblies in the same process.
/// </summary>
internal sealed class CallTargetAotMultipleRegistryAssembliesException : Exception
{
    private CallTargetAotMultipleRegistryAssembliesException(string currentRegistryAssembly, string newRegistryAssembly)
        : base($"CallTarget NativeAOT supports a single generated registry assembly per process. Current registry assembly: '{currentRegistryAssembly}', attempted registration from: '{newRegistryAssembly}'.")
    {
    }

    /// <summary>
    /// Throws a registry-mixing failure with a deterministic message.
    /// </summary>
    /// <param name="currentRegistryAssembly">The process-wide registry identity that was already accepted.</param>
    /// <param name="newRegistryAssembly">The new registry identity that attempted to register.</param>
    [DebuggerHidden]
    [DoesNotReturn]
    internal static void Throw(string currentRegistryAssembly, string newRegistryAssembly)
    {
        throw new CallTargetAotMultipleRegistryAssembliesException(currentRegistryAssembly, newRegistryAssembly);
    }
}
