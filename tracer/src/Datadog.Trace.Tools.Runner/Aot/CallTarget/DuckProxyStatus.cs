// <copyright file="DuckProxyStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

internal enum DuckProxyStatus
{
    /// <summary>The DuckType AOT registry has a proxy for the pair.</summary>
    Available,

    /// <summary>Dynamic duck typing fails for the pair: the registration records the failure.</summary>
    Failure,

    /// <summary>First pass: the proxy is being collected; the binding goes on to find the other proxies it needs.</summary>
    Pending,

    /// <summary>No proxy can be named at build time (open generic target, not generated yet): the shape is deferred.</summary>
    Unavailable,
}
#endif
