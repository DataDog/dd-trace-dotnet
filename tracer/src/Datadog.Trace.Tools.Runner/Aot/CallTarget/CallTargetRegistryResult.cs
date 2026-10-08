// <copyright file="CallTargetRegistryResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// What the CallTarget registrations of one assembly bind (B5): one entry per handler delegate registered.
/// </summary>
internal sealed class CallTargetRegistryResult
{
    public int Registrations { get; set; }

    public int Bound { get; set; }

    public int NoMethod { get; set; }

    public int Failures { get; set; }

    public int Deferred { get; set; }

    public int ContinuationFactories { get; set; }

    /// <summary>Gets the shapes that aren't bound, with the reason.</summary>
    public List<string> Details { get; } = new();

    internal void Record(AdapterBindingStatus status, string method, CallTargetInvocation invocation, string? message)
    {
        switch (status)
        {
            case AdapterBindingStatus.Bound:
                Bound++;
                return;
            case AdapterBindingStatus.NoMethod:
                NoMethod++;
                return;
            case AdapterBindingStatus.Failure:
                Failures++;
                break;
            default:
                Deferred++;
                break;
        }

        Details.Add($"{status} {method} {invocation}: {message}");
    }
}
#endif
