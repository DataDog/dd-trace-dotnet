// <copyright file="DuckTypeMappingAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// A duck typing mapping a NativeAOT build generates the proxy of (see docs/development/NativeAOT.md), when the proxy type
/// can't declare it with <see cref="DuckTypeAttribute"/>: a type of another assembly (e.g. the manual API's), or a closed
/// generic type (e.g. <c>IDuckTypeTask&lt;IResult&gt;</c>). Declare it next to the code that creates the proxy.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class DuckTypeMappingAttribute(string proxyType, string proxyAssembly, string targetType, string targetAssembly) : Attribute
{
    public string ProxyType { get; } = proxyType;

    public string ProxyAssembly { get; } = proxyAssembly;

    public string TargetType { get; } = targetType;

    public string TargetAssembly { get; } = targetAssembly;
}
