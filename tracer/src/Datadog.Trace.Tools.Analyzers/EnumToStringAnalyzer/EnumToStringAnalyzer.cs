// <copyright file="EnumToStringAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.EnumToStringAnalyzer;

/// <summary>
/// DDALLOC005: Detects .ToString() calls on enum-typed expressions.
/// Enum.ToString() boxes the value and allocates a string via reflection on all runtimes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumToStringAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Diagnostics.EnumToStringRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Must be a member access like `expr.ToString()`
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        // Must be calling "ToString"
        if (memberAccess.Name.Identifier.ValueText != "ToString")
        {
            return;
        }

        // Must have no arguments (skip ToString("D"), ToString(IFormatProvider), etc.)
        if (invocation.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        // Use the semantic model to check the receiver type
        var receiverTypeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
        var receiverType = receiverTypeInfo.Type;

        if (receiverType is null || receiverType.TypeKind != TypeKind.Enum)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            Diagnostics.EnumToStringRule,
            invocation.GetLocation(),
            receiverType.Name);

        context.ReportDiagnostic(diagnostic);
    }
}
