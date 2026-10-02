// <copyright file="InvalidFieldDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.MessagePackConstants.Diagnostics;

internal static class InvalidFieldDiagnostic
{
    internal const string Id = "DDSG004";
    private const string Title = "Invalid MessagePackField usage";
    private const string Message = "MessagePackField attribute can only be applied to const string fields with non-empty values";

    public static DiagnosticInfo CreateInfo(SyntaxNode? syntax) =>
        new(
            new DiagnosticDescriptor(
                Id,
                Title,
                Message,
                category: "CodeGeneration",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            syntax?.GetLocation());
}
