// <copyright file="TypeInfoStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Abstractions.ITypeInfo proxy structure
/// </summary>
[DuckCopy]
internal struct TypeInfoStruct
{
    /// <summary>
    /// Gets the fully qualified type name (for non-generic parameters), or the simple type name (for generic parameters).
    /// </summary>
    public string? Name;

    /// <summary>
    /// Represents information about a type.
    /// </summary>
    public AssemblyInfoStruct Assembly;
}
