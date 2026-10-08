// <copyright file="IDuckProxyProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Gives the DuckType AOT proxies the CallTarget adapters instantiate integration methods with.
/// </summary>
internal interface IDuckProxyProvider
{
    /// <param name="proxyDefinition">The constraint of the integration method's generic parameter (the duck type).</param>
    /// <param name="target">The closed type it is created for (target, argument or return value).</param>
    DuckProxy Resolve(TypeSig proxyDefinition, TypeSig target);

    /// <summary>
    /// A proxy duck typing creates at runtime from the runtime type of a value (<c>IntegrationMapper.ConvertType</c> in the
    /// slow begin path): the registry must serve <paramref name="target"/> and the types assignable to it.
    /// </summary>
    void RequestRuntimeProxy(TypeSig proxyDefinition, TypeSig target);
}
#endif
