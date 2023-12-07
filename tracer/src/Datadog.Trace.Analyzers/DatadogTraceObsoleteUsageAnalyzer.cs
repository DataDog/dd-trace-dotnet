// <copyright file="DatadogTraceObsoleteUsageAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Analyzers;

/// <summary>
/// An analyzer thta checks for usages of obsolete APIs
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DatadogTraceObsoleteUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The rule associated with calling new Tracer()
    /// </summary>
    public const string ObsoleteConstructorDiagnosticId = "DATADOG0001";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor ObsoleteConstructorDiagnostic
        = new(
            ObsoleteConstructorDiagnosticId,
            title: "Tracer() is Obsolete",
            messageFormat: "Tracer() is deprecated. Use Tracer.Instance to obtain a Tracer instance to create spans",
            description: "Tracer() is deprecated and will be removed in a future version of the tracer. Use Tracer.Instance to obtain a Tracer instance to create spans",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ObsoleteConstructorDiagnostic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(AnalyzeCompilationStart);
    }

    private static void AnalyzeCompilationStart(CompilationStartAnalysisContext? context)
    {
        if (context is null)
        {
            return;
        }

        var ddTrace = context
            .Compilation
            .ReferencedAssemblyNames
            .FirstOrDefault(x => x.Name.Equals("Datadog.Trace", StringComparison.Ordinal));

        // Only support analyzers for Datadog.Trace 2.x.x+
        if (ddTrace is null || ddTrace.Version.Major < 2)
        {
            return;
        }

        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        // context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    // private static void AnalyzeInvocation(OperationAnalysisContext context)
    // {
    //     if (context.Operation is not IInvocationOperation invocationOperation)
    //     {
    //         return;
    //     }
    //
    //     context.CancellationToken.ThrowIfCancellationRequested();
    //
    //     INamedTypeSymbol containingType = invocationOperation.TargetMethod.ContainingType;
    //
    //
    //     this.AnalyzeAssertInvocation(nunitVersion, context, invocationOperation);
    // }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        if (context.Operation is not IObjectCreationOperation { Constructor: { ContainingType: { } type } ctor } creationOperation)
        {
            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();

        if (type is { Name: "Tracer", ContainingNamespace: { Name: "Trace", ContainingNamespace: { Name: "Datadog", ContainingNamespace.IsGlobalNamespace: true } } })
        {
            // We're in a Datadog.Trace.Tracer constructor
            if (ctor.Parameters.IsDefaultOrEmpty)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor: ObsoleteConstructorDiagnostic,
                        location: creationOperation.Syntax.GetLocation()));
            }
        }
    }

    // private static void AnalyzeAssertInvocation(OperationAnalysisContext context, IInvocationOperation assertOperation)
    // {
    //     var
    //     var methodSymbol = assertOperation.TargetMethod;
    //
    //     if (ClassicModelAssertUsageAnalyzer.NameToDescriptor.TryGetValue(methodSymbol.Name, out DiagnosticDescriptor? descriptor))
    //     {
    //         context.ReportDiagnostic(Diagnostic.Create(
    //             descriptor,
    //             assertOperation.Syntax.GetLocation(),
    //             ClassicModelAssertUsageAnalyzer.GetProperties(methodSymbol)));
    //     }
    // }
}
