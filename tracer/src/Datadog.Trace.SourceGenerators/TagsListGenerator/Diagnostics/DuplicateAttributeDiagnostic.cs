// <copyright file="DuplicateAttributeDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics
{
    internal static class DuplicateAttributeDiagnostic
    {
        // internal for testing
        internal const string Id = "TL2";
        private const string Message = "You may not use both [Tag] and [Metric]";
        private const string Title = "Duplicate attributes";

        public static Diagnostic Create(SyntaxNode? currentNode, SyntaxNode? additionalLocation) =>
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    Id,
                    Title,
                    Message,
                    category: Constants.Usage,
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                currentNode?.GetLocation(),
                additionalLocations: additionalLocation is null ? null : new[] { additionalLocation.GetLocation() });
    }
}
