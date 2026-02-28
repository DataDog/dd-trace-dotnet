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
    /// <summary>
    /// Initializes a new instance of the <see cref="DuckCopyAttribute"/> class.
    /// </summary>
    public DuckCopyAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckCopyAttribute"/> class.
    /// </summary>
    /// <param name="targetType">The target type value.</param>
    /// <param name="targetAssembly">The target assembly value.</param>
    public DuckCopyAttribute(string targetType, string targetAssembly)
    {
        TargetType = targetType;
        TargetAssembly = targetAssembly;
    }

    /// <summary>
    /// Gets or sets target type.
    /// </summary>
    /// <value>The target type value.</value>
    public string? TargetType { get; set; }

    /// <summary>
    /// Gets or sets target assembly.
    /// </summary>
    /// <value>The target assembly value.</value>
    public string? TargetAssembly { get; set; }
}
