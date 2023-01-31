// <copyright file="NamingProblemDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.PublicApi.Diagnostics;

internal static class NamingProblemDiagnostic
{
    internal const string Id = "PA4";
    private const string Message = "Field names should start with '_' or end with 'Internal'";
    private const string Title = "Unknown naming";

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
