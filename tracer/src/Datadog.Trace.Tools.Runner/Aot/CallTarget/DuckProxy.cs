// <copyright file="DuckProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The generated proxy a duck typing constraint of an integration method binds to, for one target type: what
/// <c>DuckType.GetOrCreateProxyType</c> returns to <c>IntegrationMapper</c>.
/// </summary>
internal sealed class DuckProxy
{
    private DuckProxy(DuckProxyStatus status, string? message, TypeDef? type, MethodDef? constructor)
    {
        Status = status;
        Message = message;
        Type = type;
        Constructor = constructor;
    }

    public DuckProxyStatus Status { get; }

    public string? Message { get; }

    /// <summary>Gets the proxy type, in the DuckType AOT registry module.</summary>
    public TypeDef? Type { get; }

    /// <summary>
    /// Gets the constructor <c>IntegrationMapper.WriteCreateNewProxyInstance</c> calls: <c>(instance)</c>, or the internal
    /// <c>(instance, Type)</c> one of a proxy that also serves other runtime types.
    /// </summary>
    public MethodDef? Constructor { get; }

    public bool ReportsTargetType => Constructor?.MethodSig.Params.Count == 2;

    public static DuckProxy Available(TypeDef type, MethodDef constructor) => new(DuckProxyStatus.Available, null, type, constructor);

    public static DuckProxy Failure(string message) => new(DuckProxyStatus.Failure, message, null, null);

    public static DuckProxy Pending() => new(DuckProxyStatus.Pending, "collecting the proxies", null, null);

    public static DuckProxy Unavailable(string reason) => new(DuckProxyStatus.Unavailable, reason, null, null);
}
#endif
