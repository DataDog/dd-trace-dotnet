// <copyright file="AssemblyInfoStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Abstractions.IAssemblyInfo proxy structure
/// </summary>
[DuckCopy]
internal struct AssemblyInfoStruct
{
    /// <summary>
    /// Gets the on-disk location of the assembly under test.
    /// </summary>
    public string? AssemblyPath;

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    public string? Name;
}
