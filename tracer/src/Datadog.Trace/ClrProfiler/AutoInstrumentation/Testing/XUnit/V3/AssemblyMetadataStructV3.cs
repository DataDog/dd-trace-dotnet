// <copyright file="AssemblyMetadataStructV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// IAssemblyMetadata proxy
/// </summary>
[DuckCopy]
internal struct AssemblyMetadataStructV3
{
    /// <summary>
    /// Gets the assembly name. May return a simple assembly name (i.e., "mscorlib"), or may return a
    /// fully qualified name (i.e., "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089").
    /// </summary>
    public string AssemblyName;

    /// <summary>
    /// Gets the on-disk location of the assembly under test.
    /// </summary>
    public string AssemblyPath;
}
