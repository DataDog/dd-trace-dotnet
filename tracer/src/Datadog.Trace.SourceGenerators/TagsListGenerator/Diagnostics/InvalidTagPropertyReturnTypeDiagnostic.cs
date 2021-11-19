// <copyright file="InvalidTagPropertyReturnTypeDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics
{
    internal static class InvalidTagPropertyReturnTypeDiagnostic
    {
        internal const string Id = "TL3";
        private const string Message = "A tag property must return a string";
        private const string Title = "Invalid return type";

        public static Diagnostic Create(SyntaxNode? currentNode) =>
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    Id, Title, Message, category: Constants.Usage, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true),
                currentNode?.GetLocation());
    }
}
