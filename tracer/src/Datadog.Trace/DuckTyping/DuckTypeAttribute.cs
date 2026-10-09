// <copyright file="DuckTypeAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// The type is used as a duck type of the target type: a NativeAOT build generates the proxy (see
/// docs/development/NativeAOT.md). The duck types of CallTarget integration constraints don't need it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
internal sealed class DuckTypeAttribute(string targetType, string targetAssembly) : Attribute
{
    public string? TargetType { get; set; } = targetType;

    public string? TargetAssembly { get; set; } = targetAssembly;

    /// <summary>
    /// Gets or sets a value indicating whether the proxy of the target type also serves the classes that derive from it (or
    /// implement it), when the members of the duck type are the ones they inherit (e.g. the responses of a client library).
    /// </summary>
    public bool IncludeDerivedTypes { get; set; }
}
