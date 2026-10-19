// <copyright file="AdapterLoad.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// How an adapter loads one parameter of the integration method, as <c>IntegrationMapper</c> emits it.
/// </summary>
internal sealed class AdapterLoad
{
    private AdapterLoad(AdapterLoadKind kind, int index, TypeSig? type, TypeSig? boxType, DuckProxy? proxy = null, TypeSig? proxySource = null)
    {
        Kind = kind;
        Index = index;
        Type = type;
        BoxType = boxType;
        Proxy = proxy;
        ProxySource = proxySource;
    }

    public AdapterLoadKind Kind { get; }

    /// <summary>Gets the adapter parameter, or the element of the arguments array.</summary>
    public int Index { get; }

    /// <summary>Gets the type of <c>ldobj</c>, <c>unbox.any</c> or <c>IntegrationMapper.ConvertType</c>.</summary>
    public TypeSig? Type { get; }

    /// <summary>Gets the value type (or generic parameter) boxed for a reference type parameter.</summary>
    public TypeSig? BoxType { get; }

    /// <summary>Gets the duck typing proxy the loaded value is wrapped in (<c>IntegrationMapper.WriteCreateNewProxyInstance</c>).</summary>
    public DuckProxy? Proxy { get; }

    /// <summary>Gets the type of the value the proxy wraps (boxed for a value type, reported as <c>IDuckType.Type</c>).</summary>
    public TypeSig? ProxySource { get; }

    public static AdapterLoad Argument(int index, TypeSig? boxType = null) => new(AdapterLoadKind.Argument, index, null, boxType);

    public static AdapterLoad ArgumentValue(int index, TypeSig type, TypeSig? boxType = null) => new(AdapterLoadKind.ArgumentValue, index, type, boxType);

    public static AdapterLoad Proxied(int index, TypeSig? valueType, DuckProxy proxy, TypeSig source)
        => new(valueType is null ? AdapterLoadKind.Argument : AdapterLoadKind.ArgumentValue, index, valueType, null, proxy, source);

    public static AdapterLoad ArrayElement(int index) => new(AdapterLoadKind.ArrayElement, index, null, null);

    public static AdapterLoad ArrayElementUnbox(int index, TypeSig type) => new(AdapterLoadKind.ArrayElementUnbox, index, type, null);

    public static AdapterLoad ArrayElementConvert(int index, TypeSig type) => new(AdapterLoadKind.ArrayElementConvert, index, type, null);
}
#endif
