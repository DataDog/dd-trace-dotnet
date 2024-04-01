// <copyright file="DoNotCapturePrimaryConstructorParametersAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// This code is based on the analyzer provided here: https://github.com/dotnet/roslyn-analyzers/blob/4af06010a6699f5ee8f8d5a6c6091ba23f8fceeb/src/Roslyn.Diagnostics.Analyzers/CSharp/CSharpDoNotCapturePrimaryContructorParameters.cs
// Licensed as:
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Tools.Analyzers.PrimaryConstructorAnalyzer;

/// <summary>
/// Analyzer that prevents you using primary constructor parameters in a way that captures them into member state.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DoNotCapturePrimaryConstructorParametersAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic ID displayed in error messages
    /// </summary>
    public const string DiagnosticId = "DD0003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not capture primary constructor parameters",
        "Primary constructor parameter '{0}' should not be implicitly captured",
        "Maintainability",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Primary constructor parameters should not be implicitly captured. Manually assign them to fields at the start of the type.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.ParameterReference);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        var operation = (IParameterReferenceOperation)context.Operation;

        if (SymbolEqualityComparer.Default.Equals(operation.Parameter.ContainingSymbol, context.ContainingSymbol) || operation.Parameter.ContainingSymbol is not IMethodSymbol { MethodKind: MethodKind.Constructor })
        {
            // We're in the primary constructor itself, so no capture.
            // Or, this isn't a primary constructor parameter at all.
            return;
        }

        IOperation rootOperation = operation;
        for (; rootOperation.Parent != null; rootOperation = rootOperation.Parent)
        {
        }

        if (rootOperation is IPropertyInitializerOperation or IFieldInitializerOperation)
        {
            // This is an explicit capture into member state. That's fine.
            return;
        }

        // This must be a capture. Error
        context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), operation.Parameter.Name));
    }
}
