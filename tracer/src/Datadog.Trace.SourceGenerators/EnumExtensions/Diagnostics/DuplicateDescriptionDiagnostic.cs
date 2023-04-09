// <copyright file="DuplicateDescriptionDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.EnumExtensions.Diagnostics;

internal static class DuplicateDescriptionDiagnostic
{
    internal const string Id = "EE1";
    private const string Message = "This description has already been used";
    private const string Title = "Duplicate description";

    public static DiagnosticInfo CreateInfo(SyntaxNode? currentNode)
        => new(
            new DiagnosticDescriptor(
                Id,
                Title,
                Message,
                category: SourceGenerators.Constants.Usage,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            currentNode?.GetLocation());
}
