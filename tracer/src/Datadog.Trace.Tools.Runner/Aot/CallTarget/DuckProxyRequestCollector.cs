// <copyright file="DuckProxyRequestCollector.cs" company="Datadog">
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
/// First pass of the registrations: records the proxies the adapters need, which the DuckType AOT registry generates.
/// </summary>
internal sealed class DuckProxyRequestCollector : IDuckProxyProvider
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public List<(TypeSig ProxyDefinition, TypeSig Target)> Requests { get; } = new();

    /// <summary>Gets the requests of proxies looked up by runtime type, which also need the aliases of the assignable types.</summary>
    public List<(TypeSig ProxyDefinition, TypeSig Target)> RuntimeRequests { get; } = new();

    public DuckProxy Resolve(TypeSig proxyDefinition, TypeSig target)
    {
        if (_seen.Add($"{proxyDefinition.AssemblyQualifiedName}|{target.AssemblyQualifiedName}"))
        {
            Requests.Add((proxyDefinition, target));
        }

        return DuckProxy.Pending();
    }

    public void RequestRuntimeProxy(TypeSig proxyDefinition, TypeSig target)
    {
        if (!target.ContainsGenericParameter && _seen.Add($"runtime|{proxyDefinition.AssemblyQualifiedName}|{target.AssemblyQualifiedName}"))
        {
            RuntimeRequests.Add((proxyDefinition, target));
        }
    }
}
#endif
