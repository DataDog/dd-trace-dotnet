// <copyright file="DuckCopyAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Duck copy struct attribute
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
internal sealed class DuckCopyAttribute : Attribute
{
    public DuckCopyAttribute()
    {
    }

    public DuckCopyAttribute(string targetType, string targetAssembly)
    {
        TargetType = targetType;
        TargetAssembly = targetAssembly;
    }

    public string? TargetType { get; set; }

    public string? TargetAssembly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the copy of the target type also serves the classes that derive from it, in a
    /// NativeAOT build (see <see cref="DuckTypeAttribute.IncludeDerivedTypes"/>).
    /// </summary>
    public bool IncludeDerivedTypes { get; set; }
}
