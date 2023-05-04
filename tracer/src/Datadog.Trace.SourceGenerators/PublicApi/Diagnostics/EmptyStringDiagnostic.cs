// <copyright file="EmptyStringDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.PublicApi.Diagnostics
{
    internal static class EmptyStringDiagnostic
    {
        internal const string Id = "PA5";
        private const string Message = "The string value must not be null or empty";
        private const string Title = "Invalid string value";

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
