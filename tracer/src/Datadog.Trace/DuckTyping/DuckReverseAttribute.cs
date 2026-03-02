// <copyright file="DuckReverseAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Declares a reverse duck-typing mapping for a proxy contract type.
/// </summary>
/// <remarks>
/// This attribute binds a reverse contract type to a specific delegation target type/assembly pair
/// so the AOT mapping discovery pipeline can materialize explicit reverse mappings.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
internal sealed class DuckReverseAttribute(string targetType, string targetAssembly) : Attribute
{
    /// <summary>
    /// Gets or sets the reverse delegation target type name.
    /// </summary>
    public string? TargetType { get; set; } = targetType;

    /// <summary>
    /// Gets or sets the reverse delegation target assembly name.
    /// </summary>
    public string? TargetAssembly { get; set; } = targetAssembly;
}
