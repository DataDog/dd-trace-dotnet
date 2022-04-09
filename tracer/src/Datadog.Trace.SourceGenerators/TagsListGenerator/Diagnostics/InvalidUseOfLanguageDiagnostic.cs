﻿// <copyright file="InvalidUseOfLanguageDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics;

internal static class InvalidUseOfLanguageDiagnostic
{
    internal const string Id = "TL6";
    private const string Message = "You must not use 'language' as a key value";
    private const string Title = "Invalid use of language";

    public static Diagnostic Create(SyntaxNode? currentNode) =>
        Diagnostic.Create(
            new DiagnosticDescriptor(
                Id, Title, Message, category: SourceGenerators.Constants.Usage, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true),
            currentNode?.GetLocation());
}
