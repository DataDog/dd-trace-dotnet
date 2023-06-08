// <copyright file="MissingConfigKeyAttributeDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TracerSettingsSnapshot.Diagnostics;

internal static class MissingConfigKeyAttributeDiagnostic
{
    internal const string Id = "TS1";
    private const string Message = "The Settable property must be annotated with a ConfigKeyAttribute";
    private const string Title = "Missing [ConfigKey]";

    public static DiagnosticInfo CreateInfo(SyntaxNode? currentNode)
        => new(
            new DiagnosticDescriptor(
                Id,
                Title,
                Message,
                category: SourceGenerators.Constants.Usage,
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            currentNode?.GetLocation());
}
