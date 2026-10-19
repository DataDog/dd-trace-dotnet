// <copyright file="DuckReverseDelegationAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// The type is the delegation type of reverse proxies of a type of a library (<c>DuckType.CreateReverse(typeToDeriveFrom,
/// instance)</c>): a NativeAOT build generates the reverse proxy (see docs/development/NativeAOT.md). <see cref="DuckReverseAttribute"/>
/// declares it on the type to derive from instead, when it isn't a library type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class DuckReverseDelegationAttribute(string typeToDeriveFrom, string typeToDeriveFromAssembly) : Attribute
{
    public string? TypeToDeriveFrom { get; set; } = typeToDeriveFrom;

    public string? TypeToDeriveFromAssembly { get; set; } = typeToDeriveFromAssembly;
}
