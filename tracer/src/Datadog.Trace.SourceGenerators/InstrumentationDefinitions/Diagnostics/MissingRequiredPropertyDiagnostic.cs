// <copyright file="MissingRequiredPropertyDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics
{
    internal static class MissingRequiredPropertyDiagnostic
    {
        // internal for testing
        internal const string Id = "ID1";
        private const string Title = "Missing required property";

        public static Diagnostic Create(SyntaxNode? currentNode, string property, string otherProperty) =>
            Create(currentNode?.GetLocation(), $"You must set {property} or {otherProperty}");

        public static Diagnostic Create(SyntaxNode? currentNode, string property) =>
            Create(currentNode?.GetLocation(), $"You must set {property}");

        private static Diagnostic Create(Location? location, string message) =>
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    Id,
                    Title,
                    message,
                    category: TagsListGenerator.Constants.Usage,
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                location);
    }
}
