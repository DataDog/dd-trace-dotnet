// <copyright file="SpanIndexOfAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.SpanIndexOfAnalyzer;

/// <summary>
/// An analyzer that flags <c>Span&lt;T&gt;</c>/<c>ReadOnlySpan&lt;T&gt;.IndexOf(...)</c> calls whose result is only
/// compared against a sentinel value (0 or -1) to test for containment, and suggests <c>Contains(...)</c> instead.
/// <para>
/// This mirrors, for spans, review feedback repeatedly given (and accepted) on hand-rolled span-based parsers in
/// this repository: those parsers deliberately use <c>ReadOnlySpan&lt;char&gt;</c> to avoid string allocations,
/// but then partly undermine that intent by writing <c>span.IndexOf(x) &gt;= 0</c> instead of
/// <c>span.Contains(x)</c>. The existing Microsoft.CodeAnalysis.NetAnalyzers rule CA2249 covers exactly this
/// pattern for <c>string.IndexOf</c> (and is already enabled as an error in this repo's .editorconfig), but it
/// does not cover the <c>Span&lt;T&gt;</c>/<c>ReadOnlySpan&lt;T&gt;</c> overloads, which is the API shape these
/// hand-rolled parsers actually use.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpanIndexOfAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Diagnostics.UseContainsInsteadOfIndexOfRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeBinaryExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;

        if (!TryGetIndexOfInvocationAndSentinel(binary, out var invocation, out var memberAccess, out var sentinelValue))
        {
            return;
        }

        // The sentinel comparison only makes sense as a containment check when the comparison is one of:
        //   indexOf >= 0 / 0 <= indexOf        (found)
        //   indexOf > -1  / -1 < indexOf        (found)
        //   indexOf == -1 / -1 == indexOf       (not found)
        //   indexOf != -1 / -1 != indexOf       (found, inverted)
        //   indexOf < 0   / 0 > indexOf         (not found)
        if (!IsRecognizedSentinelComparison(binary, invocation, sentinelValue))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: "IndexOf" })
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;

        if (!IsSpanLikeType(receiverType))
        {
            return;
        }

        var spanTypeName = receiverType!.Name;
        var diagnostic = Diagnostic.Create(Diagnostics.UseContainsInsteadOfIndexOfRule, binary.GetLocation(), spanTypeName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsSpanLikeType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol
        {
            Name: "Span" or "ReadOnlySpan",
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
        };
    }

    private static bool TryGetIndexOfInvocationAndSentinel(
        BinaryExpressionSyntax binary,
        out InvocationExpressionSyntax invocation,
        out MemberAccessExpressionSyntax memberAccess,
        out int sentinelValue)
    {
        if (TryGetIndexOfInvocation(binary.Left, out invocation, out memberAccess) && TryGetSentinelLiteral(binary.Right, out sentinelValue))
        {
            return true;
        }

        if (TryGetIndexOfInvocation(binary.Right, out invocation, out memberAccess) && TryGetSentinelLiteral(binary.Left, out sentinelValue))
        {
            return true;
        }

        invocation = null!;
        memberAccess = null!;
        sentinelValue = 0;
        return false;
    }

    private static bool TryGetIndexOfInvocation(ExpressionSyntax expression, out InvocationExpressionSyntax invocation, out MemberAccessExpressionSyntax memberAccess)
    {
        if (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "IndexOf" } candidateMemberAccess } candidate)
        {
            invocation = candidate;
            memberAccess = candidateMemberAccess;
            return true;
        }

        invocation = null!;
        memberAccess = null!;
        return false;
    }

    private static bool TryGetSentinelLiteral(ExpressionSyntax expression, out int value)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax { Token: { Value: int literalValue } } when literalValue is 0 or -1:
                value = literalValue;
                return true;

            // Unary minus on a literal, e.g. "-1"
            case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression, Operand: LiteralExpressionSyntax { Token: { Value: int operandValue } } } when operandValue == 1:
                value = -1;
                return true;

            default:
                value = 0;
                return false;
        }
    }

    private static bool IsRecognizedSentinelComparison(BinaryExpressionSyntax binary, InvocationExpressionSyntax invocation, int sentinelValue)
    {
        var indexOfIsLeft = binary.Left == invocation;
        var kind = binary.Kind();

        return sentinelValue switch
        {
            // indexOf >= 0, or 0 <= indexOf
            0 when kind == SyntaxKind.GreaterThanOrEqualExpression && indexOfIsLeft => true,
            0 when kind == SyntaxKind.LessThanOrEqualExpression && !indexOfIsLeft => true,

            // indexOf < 0 (not found), or 0 > indexOf
            0 when kind == SyntaxKind.LessThanExpression && indexOfIsLeft => true,
            0 when kind == SyntaxKind.GreaterThanExpression && !indexOfIsLeft => true,

            // indexOf > -1 (found), or -1 < indexOf
            -1 when kind == SyntaxKind.GreaterThanExpression && indexOfIsLeft => true,
            -1 when kind == SyntaxKind.LessThanExpression && !indexOfIsLeft => true,

            // indexOf == -1 / -1 == indexOf (not found)
            -1 when kind == SyntaxKind.EqualsExpression => true,

            // indexOf != -1 / -1 != indexOf (found)
            -1 when kind == SyntaxKind.NotEqualsExpression => true,

            _ => false,
        };
    }
}
