// <copyright file="UnknownAdoNetSignatureNameDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics;

internal static class UnknownAdoNetSignatureNameDiagnostic
{
    // internal for testing
    internal const string Id = "ID3";
    private const string Title = "Unknown AdoNetTargetSignatureAttribute";

    public static DiagnosticInfo Create(LocationInfo? location, string signatureName) =>
        new(
            new DiagnosticDescriptor(
                Id,
                Title,
                $"The provided type '{signatureName}' is not a known AdoNetTargetSignature type",
                category: SourceGenerators.Constants.Usage,
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            location);
}
