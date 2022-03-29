// <copyright file="InvalidMetricPropertyReturnTypeDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics
{
    internal static class InvalidMetricPropertyReturnTypeDiagnostic
    {
        internal const string Id = "TL4";
        private const string Message = "A metric property must return a double?";
        private const string Title = "Invalid return type";

        public static Diagnostic Create(SyntaxNode? currentNode) =>
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    Id, Title, Message, category: SourceGenerators.Constants.Usage, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true),
                currentNode?.GetLocation());
    }
}
