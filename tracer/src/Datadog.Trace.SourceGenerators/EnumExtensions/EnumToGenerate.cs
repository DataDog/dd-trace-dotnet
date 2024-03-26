// <copyright file="EnumToGenerate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.EnumExtensions;

internal readonly record struct EnumToGenerate
{
    public readonly string ExtensionsName;
    public readonly string FullyQualifiedName;
    public readonly string Namespace;
    public readonly bool HasFlags;
    public readonly bool HasDescriptions;

    /// <summary>
    /// Key is the enum name.
    /// </summary>
    public readonly EquatableArray<(string Property, string? Name)> Names;

    public EnumToGenerate(
        string extensionsName,
        string ns,
        string fullyQualifiedName,
        List<(string Property, string? Name)> names,
        bool hasFlags,
        bool hasDescriptions)
    {
        ExtensionsName = extensionsName;
        Namespace = ns;
        Names = new EquatableArray<(string, string?)>(names.ToArray());
        HasFlags = hasFlags;
        FullyQualifiedName = fullyQualifiedName;
        HasDescriptions = hasDescriptions;
    }
}
