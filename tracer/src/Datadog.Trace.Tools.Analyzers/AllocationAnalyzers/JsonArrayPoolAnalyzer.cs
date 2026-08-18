// <copyright file="JsonArrayPoolAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.AllocationAnalyzers;

/// <summary>
/// Detects <c>new JsonTextReader(...)</c> and <c>new JsonTextWriter(...)</c> object creations
/// where the <c>ArrayPool</c> property is not set in an object initializer.
/// Without <c>ArrayPool</c>, Newtonsoft.Json allocates internal <c>char[]</c> buffers on every read/write.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class JsonArrayPoolAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] TargetTypeNames =
    [
        "Datadog.Trace.Vendors.Newtonsoft.Json.JsonTextReader",
        "Datadog.Trace.Vendors.Newtonsoft.Json.JsonTextWriter",
        "Newtonsoft.Json.JsonTextReader",
        "Newtonsoft.Json.JsonTextWriter",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    { get; } = ImmutableArray.Create(Diagnostics.JsonArrayPoolRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var targetTypes = TargetTypeNames
            .Select(name => context.Compilation.GetTypeByMetadataName(name))
            .Where(t => t is not null)
            .ToImmutableArray();

        if (targetTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeObjectCreation(ctx, targetTypes!),
            SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol?> targetTypes)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol createdType)
        {
            return;
        }

        var isTarget = false;
        foreach (var targetType in targetTypes)
        {
            if (SymbolEqualityComparer.Default.Equals(createdType, targetType))
            {
                isTarget = true;
                break;
            }
        }

        if (!isTarget)
        {
            return;
        }

        // Check if the object initializer sets the ArrayPool property
        if (objectCreation.Initializer is { Expressions: var expressions })
        {
            foreach (var expression in expressions)
            {
                if (expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.Text: "ArrayPool" } })
                {
                    // ArrayPool is already set
                    return;
                }
            }
        }

        var typeName = createdType.Name;
        var diagnostic = Diagnostic.Create(
            Diagnostics.JsonArrayPoolRule,
            objectCreation.GetLocation(),
            typeName);

        context.ReportDiagnostic(diagnostic);
    }
}
