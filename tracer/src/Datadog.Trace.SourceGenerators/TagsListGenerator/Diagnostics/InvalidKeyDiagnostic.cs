// <copyright file="InvalidKeyDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics
{
    internal static class InvalidKeyDiagnostic
    {
        internal const string Id = "TL1";
        private const string Message = "The key may not be null or whitespace";
        private const string Title = "Invalid Key";

        public static Diagnostic Create(SyntaxNode? currentNode) =>
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    Id, Title, Message, category: Constants.Usage, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true),
                currentNode?.GetLocation());
    }
}
