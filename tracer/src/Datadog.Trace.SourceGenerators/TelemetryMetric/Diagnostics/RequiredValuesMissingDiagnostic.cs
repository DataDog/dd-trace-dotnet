// <copyright file="RequiredValuesMissingDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.TelemetryMetric.Diagnostics
{
    internal static class RequiredValuesMissingDiagnostic
    {
        internal const string Id = "TM2";
        private const string Message = "The metric value must not be null or empty, and tag count is required";
        private const string Title = "Invalid values";

        public static Diagnostic Create(SyntaxNode? currentNode)
            => Diagnostic.Create(
                new DiagnosticDescriptor(
                    Id,
                    Title,
                    Message,
                    category: SourceGenerators.Constants.Usage,
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                currentNode?.GetLocation());

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
}
