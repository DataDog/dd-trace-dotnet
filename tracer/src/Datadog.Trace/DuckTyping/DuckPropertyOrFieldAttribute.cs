// <copyright file="DuckPropertyOrFieldAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Duck attribute where the underlying member could be a field or a property
/// </summary>
internal sealed class DuckPropertyOrFieldAttribute : DuckAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuckPropertyOrFieldAttribute"/> class.
    /// </summary>
    public DuckPropertyOrFieldAttribute()
    {
        Kind = DuckKind.PropertyOrField;
    }
}
