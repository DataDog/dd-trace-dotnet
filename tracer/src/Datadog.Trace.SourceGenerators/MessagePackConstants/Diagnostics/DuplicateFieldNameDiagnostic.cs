// <copyright file="DuplicateFieldNameDiagnostic.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.SourceGenerators.MessagePackConstants.Diagnostics;

internal static class DuplicateFieldNameDiagnostic
{
    internal const string Id = "DDSG005";
    private const string Title = "Duplicate MessagePackField name";
    private const string MessageFormat = "MessagePackField '{0}' is defined multiple times. Each field name must be unique to avoid conflicts in generated code.";

    private static readonly DiagnosticDescriptor Rule = new(
        Id,
        Title,
        MessageFormat,
        category: "CodeGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static Diagnostic Create(string fieldName) =>
        Diagnostic.Create(Rule, location: null, fieldName);
}
