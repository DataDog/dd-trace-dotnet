// <copyright file="PartialModifierIsRequiredDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.PublicApi.Diagnostics
{
    internal static class PartialModifierIsRequiredDiagnostic
    {
        internal const string Id = "PA4";
        private const string Message = "The partial modifier is required when using GeneratePublicApiAttribute";
        private const string Title = "Requires 'partial'";

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
