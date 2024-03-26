// <copyright file="InvalidVersionFormatDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics;

internal static class InvalidVersionFormatDiagnostic
{
    // internal for testing
    internal const string Id = "ID2";
    private const string Title = "Invalid version format";

    public static DiagnosticInfo Create(SyntaxNode? currentNode, string propertyName) =>
        new(
            new DiagnosticDescriptor(
                Id,
                Title,
                $"{propertyName} must be of form <major>.<minor>.<patch> with optional * for 'any'",
                category: SourceGenerators.Constants.Usage,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            currentNode?.GetLocation());
}
